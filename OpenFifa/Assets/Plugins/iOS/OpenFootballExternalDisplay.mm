// External display support for the iPad build.
//
// Unity 6.3's iOS shell uses the UIScene lifecycle but only declares the "application" scene role.
// Without a scene configuration for UIWindowSceneSessionRoleExternalDisplayNonInteractive, iPadOS
// mirrors the iPad onto a connected monitor (4:3, iPad resolution, letterboxed) and Unity's external
// display window has no UIWindowScene to live in.
//
// ExternalDisplayPostprocess (Editor) adds that role to Info.plist with OFExternalDisplayScene as the
// delegate. This delegate makes sure Unity's DisplayManager knows about the screen and re-homes Unity's
// external window into the scene once both exist (whichever comes first: Display.Activate() from C#,
// or the scene connecting).

#import <UIKit/UIKit.h>
#include "UnityAppController.h"
#include "Unity/DisplayManager.h"

static BOOL OFUnityReady(void)
{
    UnityAppController* app = GetAppController();
    return app != nil && app.engineLoadState >= kUnityEngineLoadStateCoreInitialized;
}

static UIWindowScene* OFExternalScene(void)
{
    for (UIScene* scene in UIApplication.sharedApplication.connectedScenes)
    {
        if (![scene isKindOfClass: [UIWindowScene class]])
            continue;
        if ([scene.session.role isEqualToString: UIWindowSceneSessionRoleExternalDisplayNonInteractive])
            return (UIWindowScene*)scene;
    }
    return nil;
}

// Register the scene's screen with Unity (if the legacy UIScreen notification did not) and move a
// shown Unity window for that screen into the scene.
static void OFAttachScene(UIWindowScene* scene)
{
    if (scene == nil || !OFUnityReady())
        return;

    UIScreen* screen = scene.screen;
    DisplayManager* dm = [DisplayManager Instance];
    if (![dm displayAvailable: screen])
    {
        // DisplayManager listens for this notification; registering is idempotent.
        [[NSNotificationCenter defaultCenter] postNotificationName: UIScreenDidConnectNotification object: screen];
    }

    DisplayConnection* conn = dm[screen];
    UIWindow* window = conn.window;
    if (window != nil && !window.hidden && window.windowScene != scene)
        window.windowScene = scene;
}

@interface OFExternalDisplayScene : UIResponder<UIWindowSceneDelegate>
@property (strong, nonatomic) UIWindow* window;
@end

@implementation OFExternalDisplayScene

- (void)scene:(UIScene*)scene willConnectToSession:(UISceneSession*)session options:(UISceneConnectionOptions*)connectionOptions
{
    if ([scene isKindOfClass: [UIWindowScene class]])
        OFAttachScene((UIWindowScene*)scene);
}

- (void)sceneWillEnterForeground:(UIScene*)scene
{
    if ([scene isKindOfClass: [UIWindowScene class]])
        OFAttachScene((UIWindowScene*)scene);
}

- (void)sceneDidDisconnect:(UIScene*)scene
{
    if (![scene isKindOfClass: [UIWindowScene class]] || !OFUnityReady())
        return;
    UIScreen* screen = ((UIWindowScene*)scene).screen;
    if ([[DisplayManager Instance] displayAvailable: screen])
        [[NSNotificationCenter defaultCenter] postNotificationName: UIScreenDidDisconnectNotification object: screen];
}

@end

extern "C" {

// Called from C# right after Display.Activate(): covers the case where the scene connected before
// Unity created its external window.
void OFExternalDisplay_AttachWindow(void)
{
    OFAttachScene(OFExternalScene());
}

// 1 when iPadOS has handed the app an external-display scene (i.e. we are not being mirrored).
int OFExternalDisplay_HasScene(void)
{
    return OFExternalScene() != nil ? 1 : 0;
}

// Highest refresh rate the external screen supports, 0 when there is none.
int OFExternalDisplay_MaxFramesPerSecond(void)
{
    UIWindowScene* scene = OFExternalScene();
    if (scene != nil)
        return (int)scene.screen.maximumFramesPerSecond;
    for (UIScreen* screen in [UIScreen screens])
        if (screen != [UIScreen mainScreen])
            return (int)screen.maximumFramesPerSecond;
    return 0;
}

// Native pixel size of the external screen's preferred mode (0x0 when none).
void OFExternalDisplay_NativeSize(int* width, int* height)
{
    *width = 0;
    *height = 0;
    UIWindowScene* scene = OFExternalScene();
    UIScreen* screen = scene != nil ? scene.screen : nil;
    if (screen == nil)
        for (UIScreen* s in [UIScreen screens])
            if (s != [UIScreen mainScreen]) { screen = s; break; }
    if (screen == nil)
        return;
    UIScreenMode* mode = screen.preferredMode ? screen.preferredMode : screen.currentMode;
    *width = (int)mode.size.width;
    *height = (int)mode.size.height;
}

}
