// External display support for the iPad build.
//
// Unity 6.3's iOS shell uses the UIScene lifecycle but only declares the "application" scene role.
// Without a scene configuration for UIWindowSceneSessionRoleExternalDisplayNonInteractive, iPadOS
// mirrors the iPad onto a connected monitor (4:3, iPad resolution, letterboxed) and Unity's external
// display window has no UIWindowScene to live in.
//
// ExternalDisplayPostprocess (Editor) adds that role to Info.plist with OFExternalDisplayScene as the
// delegate. This delegate makes sure Unity's DisplayManager knows the screen and re-homes Unity's
// external window into the scene once both exist. C# (ExternalDisplayManager) calls
// OFExternalDisplay_AttachWindow after Display.Activate and keeps retrying, a frame at a time and with a
// cap, until OFExternalDisplay_IsAttached reports success, because Unity may create the window a frame
// later. Connects normally reach Unity through UIKit's UIScreen notifications; when they don't, the screen is
// registered directly here, and sceneDidDisconnect: hands the matching disconnect to Unity so the display
// list never keeps a monitor that is gone. Nothing here posts UIKit notifications.

#import <UIKit/UIKit.h>
#include "UnityAppController.h"
#include "Unity/DisplayManager.h"

// Implemented by the trampoline's DisplayManager but not declared in its header.
@interface DisplayManager (OFExternalDisplay)
- (void)registerScreen:(UIScreen*)screen;
- (void)screenDidDisconnect:(NSNotification*)notification;
@end

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

// Make sure Unity knows the scene's screen, then move a shown Unity window for it into the scene.
// Returns YES when Unity's window for this screen is shown inside the scene.
static BOOL OFAttachScene(UIWindowScene* scene)
{
    if (scene == nil || !OFUnityReady())
        return NO;

    UIScreen* screen = scene.screen;
    DisplayManager* dm = [DisplayManager Instance];
    if (![dm displayAvailable: screen] && [dm respondsToSelector: @selector(registerScreen:)])
    {
        [dm registerScreen: screen];
        [dm updateDisplayListCacheInUnity];
    }

    DisplayConnection* conn = dm[screen];
    UIWindow* window = conn.window;
    if (window == nil || window.hidden)
        return NO;
    if (window.windowScene != scene)
        window.windowScene = scene;
    return window.windowScene == scene;
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

// A non-interactive external-display scene is torn down when its monitor goes away. If Unity still lists the
// screen (we registered it ourselves, so UIKit may never post the matching disconnect), run Unity's own
// disconnect handler directly so Display.displays shrinks and the C# side moves the game back to the iPad.
- (void)sceneDidDisconnect:(UIScene*)scene
{
    if (![scene isKindOfClass: [UIWindowScene class]] || !OFUnityReady())
        return;
    UIScreen* screen = ((UIWindowScene*)scene).screen;
    DisplayManager* dm = [DisplayManager Instance];
    if (screen != nil && screen != UIScreen.mainScreen && [dm displayAvailable: screen] &&
        [dm respondsToSelector: @selector(screenDidDisconnect:)])
    {
        [dm screenDidDisconnect: [NSNotification notificationWithName: UIScreenDidDisconnectNotification object: screen]];
    }
}

@end

extern "C" {

// Called from C# after Display.Activate() and retried until attached.
int OFExternalDisplay_AttachWindow(void)
{
    return OFAttachScene(OFExternalScene()) ? 1 : 0;
}

// 1 when Unity's external window lives in the external-display scene (i.e. the monitor shows the game).
int OFExternalDisplay_IsAttached(void)
{
    UIWindowScene* scene = OFExternalScene();
    if (scene == nil || !OFUnityReady())
        return 0;
    DisplayConnection* conn = [DisplayManager Instance][scene.screen];
    return conn.window != nil && !conn.window.hidden && conn.window.windowScene == scene ? 1 : 0;
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
    return scene != nil ? (int)scene.screen.maximumFramesPerSecond : 0;
}

// Native pixel size of the external screen's preferred mode (0x0 when none).
void OFExternalDisplay_NativeSize(int* width, int* height)
{
    *width = 0;
    *height = 0;
    UIWindowScene* scene = OFExternalScene();
    if (scene == nil)
        return;
    UIScreenMode* mode = scene.screen.preferredMode ? scene.screen.preferredMode : scene.screen.currentMode;
    *width = (int)mode.size.width;
    *height = (int)mode.size.height;
}


}
