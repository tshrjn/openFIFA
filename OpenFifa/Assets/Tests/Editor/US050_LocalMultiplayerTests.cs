using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US050")]
    [Category("Input")]
    public class US050_LocalMultiplayerTests
    {
        // === ControlSchemeAssigner Tests ===

        [Test]
        public void ControlSchemeAssigner_Player1_DefaultKeyboardMouse()
        {
            var assigner = new ControlSchemeAssigner();
            Assert.AreEqual(ControlScheme.KeyboardMouse, assigner.GetScheme(0));
        }

        [Test]
        public void ControlSchemeAssigner_Player2_DefaultGamepad()
        {
            var assigner = new ControlSchemeAssigner();
            Assert.AreEqual(ControlScheme.Gamepad, assigner.GetScheme(1));
        }

        [Test]
        public void ControlSchemeAssigner_DefaultSchemes_AreSeparate()
        {
            var assigner = new ControlSchemeAssigner();
            Assert.IsTrue(assigner.AreSchemesSeparate());
        }

        [Test]
        public void ControlSchemeAssigner_BothGamepads_NotSeparate()
        {
            var assigner = new ControlSchemeAssigner();
            assigner.SetScheme(0, ControlScheme.Gamepad);
            assigner.SetScheme(1, ControlScheme.Gamepad);
            Assert.IsFalse(assigner.AreSchemesSeparate(),
                "Both players on gamepads should not be flagged as separate schemes");
        }

        [Test]
        public void ControlSchemeAssigner_SetScheme_OverridesDefault()
        {
            var assigner = new ControlSchemeAssigner();
            assigner.SetScheme(0, ControlScheme.Gamepad);
            Assert.AreEqual(ControlScheme.Gamepad, assigner.GetScheme(0));
        }

        [Test]
        public void ControlSchemeAssigner_UnknownPlayer_ReturnsGamepad()
        {
            var assigner = new ControlSchemeAssigner();
            Assert.AreEqual(ControlScheme.Gamepad, assigner.GetScheme(99),
                "Unknown player index should default to Gamepad");
        }

        [Test]
        public void ControlSchemeAssigner_PlayerCount_ReturnsTwo()
        {
            var assigner = new ControlSchemeAssigner();
            Assert.AreEqual(2, assigner.PlayerCount,
                "Default assigner should have two player schemes registered");
        }

        // === LocalMultiplayerConfig Tests ===

        [Test]
        public void LocalMultiplayerConfig_DefaultConfig_TwoPlayers()
        {
            var config = new LocalMultiplayerConfig();
            Assert.AreEqual(2, config.HumanPlayerCount);
        }

        [Test]
        public void LocalMultiplayerConfig_DefaultConfig_AIFillsRemaining()
        {
            var config = new LocalMultiplayerConfig();
            // 5v5 = 10 players total, 2 human, 8 AI
            Assert.AreEqual(8, config.AIPlayerCount);
            Assert.AreEqual(10, config.TotalPlayerCount);
        }

        [Test]
        public void LocalMultiplayerConfig_Player1_ControlsTeamA()
        {
            var config = new LocalMultiplayerConfig();
            Assert.AreEqual(0, config.Player1TeamIndex);
        }

        [Test]
        public void LocalMultiplayerConfig_Player2_ControlsTeamB()
        {
            var config = new LocalMultiplayerConfig();
            Assert.AreEqual(1, config.Player2TeamIndex);
        }

        // === DeviceInputRouter Tests ===

        [Test]
        public void DeviceInputRouter_AssignDevice_RoutesToPlayer()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0);
            Assert.AreEqual(0, router.GetOwningPlayer(100));
        }

        [Test]
        public void DeviceInputRouter_AssignMultipleDevices_RoutesIndependently()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0); // Keyboard -> Player 0
            router.AssignDevice(200, 1); // Gamepad -> Player 1
            Assert.AreEqual(0, router.GetOwningPlayer(100));
            Assert.AreEqual(1, router.GetOwningPlayer(200));
        }

        [Test]
        public void DeviceInputRouter_UnassignedDevice_ReturnsNegOne()
        {
            var router = new DeviceInputRouter();
            Assert.AreEqual(-1, router.GetOwningPlayer(999));
        }

        [Test]
        public void DeviceInputRouter_UnassignDevice_RemovesMapping()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0);
            router.UnassignDevice(100);
            Assert.AreEqual(-1, router.GetOwningPlayer(100));
        }

        [Test]
        public void DeviceInputRouter_ClearAll_RemovesAllMappings()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0);
            router.AssignDevice(200, 1);
            router.ClearAll();
            Assert.AreEqual(-1, router.GetOwningPlayer(100));
            Assert.AreEqual(-1, router.GetOwningPlayer(200));
            Assert.AreEqual(0, router.AssignedDeviceCount);
        }

        [Test]
        public void DeviceInputRouter_IsDeviceAssigned_ReturnsTrueForAssigned()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0);
            Assert.IsTrue(router.IsDeviceAssigned(100),
                "Device 100 should be reported as assigned");
        }

        [Test]
        public void DeviceInputRouter_IsDeviceAssigned_ReturnsFalseForUnassigned()
        {
            var router = new DeviceInputRouter();
            Assert.IsFalse(router.IsDeviceAssigned(999),
                "Device 999 was never assigned and should not be reported as assigned");
        }

        [Test]
        public void DeviceInputRouter_HasDeviceForPlayer_ReturnsTrueWhenAssigned()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0);
            Assert.IsTrue(router.HasDeviceForPlayer(0),
                "Player 0 should have at least one device");
        }

        [Test]
        public void DeviceInputRouter_HasDeviceForPlayer_ReturnsFalseWhenNone()
        {
            var router = new DeviceInputRouter();
            Assert.IsFalse(router.HasDeviceForPlayer(0),
                "Player 0 should have no devices initially");
        }

        [Test]
        public void DeviceInputRouter_GetDeviceForPlayer_ReturnsDeviceId()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(42, 1);
            Assert.AreEqual(42, router.GetDeviceForPlayer(1),
                "Should return device 42 for player 1");
        }

        [Test]
        public void DeviceInputRouter_GetDeviceForPlayer_ReturnsNegOneWhenNone()
        {
            var router = new DeviceInputRouter();
            Assert.AreEqual(-1, router.GetDeviceForPlayer(0),
                "Should return -1 when no device is assigned to player 0");
        }

        // === PlayerSlot Tests ===

        [Test]
        public void PlayerSlot_Constructor_SetsPropertiesCorrectly()
        {
            var slot = new PlayerSlot(0, ControlScheme.KeyboardMouse, 0);
            Assert.AreEqual(0, slot.SlotIndex);
            Assert.AreEqual(ControlScheme.KeyboardMouse, slot.Scheme);
            Assert.AreEqual(0, slot.TeamIndex);
            Assert.IsFalse(slot.IsReady, "Slot should not be ready initially");
            Assert.IsFalse(slot.IsOccupied, "Slot should not be occupied initially");
            Assert.AreEqual("Player 1", slot.DisplayName);
        }

        [Test]
        public void PlayerSlot_Player2_HasCorrectDefaults()
        {
            var slot = new PlayerSlot(1, ControlScheme.Gamepad, 1);
            Assert.AreEqual(1, slot.SlotIndex);
            Assert.AreEqual(ControlScheme.Gamepad, slot.Scheme);
            Assert.AreEqual(1, slot.TeamIndex);
            Assert.AreEqual("Player 2", slot.DisplayName);
        }

        [Test]
        public void PlayerSlot_ReadyToggle_ChangesState()
        {
            var slot = new PlayerSlot(0, ControlScheme.KeyboardMouse, 0);
            Assert.IsFalse(slot.IsReady);
            slot.IsReady = true;
            Assert.IsTrue(slot.IsReady);
            slot.IsReady = false;
            Assert.IsFalse(slot.IsReady);
        }

        // === LobbyLogic Tests ===

        [Test]
        public void LobbyLogic_InitialState_WaitingForPlayers()
        {
            var lobby = new LobbyLogic();
            Assert.AreEqual(LobbyState.WaitingForPlayers, lobby.State);
        }

        [Test]
        public void LobbyLogic_JoinSlot_OccupiesSlot()
        {
            var lobby = new LobbyLogic();
            bool result = lobby.JoinSlot(0);
            Assert.IsTrue(result, "Should successfully join slot 0");
            Assert.AreEqual(1, lobby.OccupiedSlotCount);
        }

        [Test]
        public void LobbyLogic_JoinSlot_RejectsDoubleJoin()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            bool result = lobby.JoinSlot(0);
            Assert.IsFalse(result, "Should not allow joining an already-occupied slot");
        }

        [Test]
        public void LobbyLogic_JoinSlot_RejectsInvalidIndex()
        {
            var lobby = new LobbyLogic();
            Assert.IsFalse(lobby.JoinSlot(-1), "Negative index should be rejected");
            Assert.IsFalse(lobby.JoinSlot(5), "Index >= MaxPlayers should be rejected");
        }

        [Test]
        public void LobbyLogic_JoinBothSlots_TransitionsToAllConnected()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            Assert.AreEqual(LobbyState.AllConnected, lobby.State,
                "Both slots occupied should transition to AllConnected");
        }

        [Test]
        public void LobbyLogic_LeaveSlot_FreesSlot()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.LeaveSlot(1);
            Assert.AreEqual(1, lobby.OccupiedSlotCount);
            Assert.AreEqual(LobbyState.WaitingForPlayers, lobby.State,
                "Should return to WaitingForPlayers when a slot is freed");
        }

        [Test]
        public void LobbyLogic_ToggleReady_TogglesState()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            bool ready = lobby.ToggleReady(0);
            Assert.IsTrue(ready, "First toggle should set ready to true");
            ready = lobby.ToggleReady(0);
            Assert.IsFalse(ready, "Second toggle should set ready to false");
        }

        [Test]
        public void LobbyLogic_ToggleReady_FailsForUnoccupiedSlot()
        {
            var lobby = new LobbyLogic();
            bool result = lobby.ToggleReady(0);
            Assert.IsFalse(result, "Cannot toggle ready on unoccupied slot");
        }

        [Test]
        public void LobbyLogic_AllReady_TransitionsToAllReady()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.SetReady(0, true);
            lobby.SetReady(1, true);
            Assert.AreEqual(LobbyState.AllReady, lobby.State,
                "Both players ready should transition to AllReady");
        }

        [Test]
        public void LobbyLogic_CanStartMatch_TrueWhenAllReady()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.SetReady(0, true);
            lobby.SetReady(1, true);
            Assert.IsTrue(lobby.CanStartMatch());
        }

        [Test]
        public void LobbyLogic_CanStartMatch_FalseWhenNotAllOccupied()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            lobby.SetReady(0, true);
            Assert.IsFalse(lobby.CanStartMatch(),
                "Cannot start with only one player connected");
        }

        [Test]
        public void LobbyLogic_CanStartMatch_FalseWhenNotAllReady()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.SetReady(0, true);
            // P2 not ready
            Assert.IsFalse(lobby.CanStartMatch(),
                "Cannot start when not all players are ready");
        }

        [Test]
        public void LobbyLogic_StartCountdown_BeginsCountdown()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.SetReady(0, true);
            lobby.SetReady(1, true);
            bool started = lobby.StartCountdown();
            Assert.IsTrue(started, "Countdown should start when all ready");
            Assert.AreEqual(LobbyState.CountingDown, lobby.State);
        }

        [Test]
        public void LobbyLogic_StartCountdown_FailsWhenNotReady()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            bool started = lobby.StartCountdown();
            Assert.IsFalse(started, "Countdown should not start with only one player");
        }

        [Test]
        public void LobbyLogic_TickCountdown_DecreasesTime()
        {
            var lobby = new LobbyLogic(3f);
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.SetReady(0, true);
            lobby.SetReady(1, true);
            lobby.StartCountdown();

            lobby.TickCountdown(1f);
            Assert.That(lobby.CountdownRemaining, Is.EqualTo(2f).Within(0.01f),
                "Countdown should decrease by 1 second");
        }

        [Test]
        public void LobbyLogic_TickCountdown_TriggersMatchStart()
        {
            var lobby = new LobbyLogic(1f);
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.SetReady(0, true);
            lobby.SetReady(1, true);
            lobby.StartCountdown();

            bool matchStarted = false;
            lobby.OnMatchStart += () => matchStarted = true;

            lobby.TickCountdown(1.5f);
            Assert.IsTrue(matchStarted, "Match should start when countdown reaches zero");
            Assert.AreEqual(LobbyState.Starting, lobby.State);
        }

        [Test]
        public void LobbyLogic_CancelCountdown_ReturnsToCorrectState()
        {
            var lobby = new LobbyLogic(3f);
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.SetReady(0, true);
            lobby.SetReady(1, true);
            lobby.StartCountdown();
            lobby.TickCountdown(1f);

            lobby.CancelCountdown();
            // After cancelling, should return to AllReady (since both still ready)
            Assert.AreEqual(LobbyState.AllReady, lobby.State,
                "Cancelling countdown should return to AllReady");
            Assert.That(lobby.CountdownRemaining, Is.EqualTo(3f).Within(0.01f),
                "Countdown should reset to full duration");
        }

        [Test]
        public void LobbyLogic_Reset_ClearsEverything()
        {
            var lobby = new LobbyLogic();
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.SetReady(0, true);
            lobby.SetReady(1, true);

            lobby.Reset();
            Assert.AreEqual(0, lobby.OccupiedSlotCount);
            Assert.AreEqual(0, lobby.ReadyPlayerCount);
            Assert.AreEqual(LobbyState.WaitingForPlayers, lobby.State);
        }

        [Test]
        public void LobbyLogic_StateChangedEvent_FiresOnTransition()
        {
            var lobby = new LobbyLogic();
            var stateHistory = new List<LobbyState>();
            lobby.OnStateChanged += s => stateHistory.Add(s);

            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            // Should have transitioned: WaitingForPlayers -> (still waiting after first join)
            // Then -> AllConnected after second join
            Assert.Contains(LobbyState.AllConnected, stateHistory,
                "State history should include AllConnected after both players join");
        }

        [Test]
        public void LobbyLogic_GetSlot_ReturnsCorrectSlot()
        {
            var lobby = new LobbyLogic();
            var slot0 = lobby.GetSlot(0);
            var slot1 = lobby.GetSlot(1);
            Assert.IsNotNull(slot0);
            Assert.IsNotNull(slot1);
            Assert.AreEqual(0, slot0.SlotIndex);
            Assert.AreEqual(1, slot1.SlotIndex);
        }

        [Test]
        public void LobbyLogic_GetSlot_ReturnsNullForInvalidIndex()
        {
            var lobby = new LobbyLogic();
            Assert.IsNull(lobby.GetSlot(-1));
            Assert.IsNull(lobby.GetSlot(5));
        }

        // === InputConflictDetector Tests ===

        [Test]
        public void InputConflictDetector_CanAssignDevice_TrueForFreeDevice()
        {
            var router = new DeviceInputRouter();
            var assigner = new ControlSchemeAssigner();
            var detector = new InputConflictDetector(router, assigner);

            Assert.IsTrue(detector.CanAssignDevice(100, 0),
                "Free device should be assignable to any player");
        }

        [Test]
        public void InputConflictDetector_CanAssignDevice_TrueForSamePlayer()
        {
            var router = new DeviceInputRouter();
            var assigner = new ControlSchemeAssigner();
            var detector = new InputConflictDetector(router, assigner);

            router.AssignDevice(100, 0);
            Assert.IsTrue(detector.CanAssignDevice(100, 0),
                "Device already assigned to player 0 should be re-assignable to player 0");
        }

        [Test]
        public void InputConflictDetector_CanAssignDevice_FalseForOtherPlayer()
        {
            var router = new DeviceInputRouter();
            var assigner = new ControlSchemeAssigner();
            var detector = new InputConflictDetector(router, assigner);

            router.AssignDevice(100, 0);
            Assert.IsFalse(detector.CanAssignDevice(100, 1),
                "Device assigned to player 0 should not be assignable to player 1");
        }

        [Test]
        public void InputConflictDetector_IsDeviceAvailable_TrueForUnassigned()
        {
            var router = new DeviceInputRouter();
            var assigner = new ControlSchemeAssigner();
            var detector = new InputConflictDetector(router, assigner);

            Assert.IsTrue(detector.IsDeviceAvailable(999),
                "Unassigned device should be available");
        }

        [Test]
        public void InputConflictDetector_IsDeviceAvailable_FalseForAssigned()
        {
            var router = new DeviceInputRouter();
            var assigner = new ControlSchemeAssigner();
            var detector = new InputConflictDetector(router, assigner);

            router.AssignDevice(100, 0);
            Assert.IsFalse(detector.IsDeviceAvailable(100),
                "Assigned device should not be available");
        }

        [Test]
        public void InputConflictDetector_IsConflictFree_TrueWhenBothHaveDevices()
        {
            var router = new DeviceInputRouter();
            var assigner = new ControlSchemeAssigner();
            var detector = new InputConflictDetector(router, assigner);

            router.AssignDevice(100, 0);
            router.AssignDevice(200, 1);
            Assert.IsTrue(detector.IsConflictFree(),
                "Both players having separate devices should be conflict-free");
        }

        [Test]
        public void InputConflictDetector_IsConflictFree_FalseWhenOnlyOneHasDevice()
        {
            var router = new DeviceInputRouter();
            var assigner = new ControlSchemeAssigner();
            var detector = new InputConflictDetector(router, assigner);

            router.AssignDevice(100, 0);
            Assert.IsFalse(detector.IsConflictFree(),
                "Only player 0 has a device; should not be conflict-free");
        }

        [Test]
        public void InputConflictDetector_ValidateConfiguration_TrueForDefaultSetup()
        {
            var router = new DeviceInputRouter();
            var assigner = new ControlSchemeAssigner();
            var detector = new InputConflictDetector(router, assigner);

            router.AssignDevice(100, 0); // Keyboard
            router.AssignDevice(200, 1); // Gamepad
            Assert.IsTrue(detector.ValidateConfiguration(),
                "Default keyboard+gamepad setup should be valid");
        }

        [Test]
        public void InputConflictDetector_ValidateConfiguration_FalseWhenSameDeviceSameScheme()
        {
            var router = new DeviceInputRouter();
            var assigner = new ControlSchemeAssigner();
            var detector = new InputConflictDetector(router, assigner);

            // Both on gamepad but same device
            assigner.SetScheme(0, ControlScheme.Gamepad);
            assigner.SetScheme(1, ControlScheme.Gamepad);
            router.AssignDevice(100, 0);
            router.AssignDevice(100, 1); // Same device reassigned — overwrites to player 1
            // Now player 0 has no device
            Assert.IsFalse(detector.ValidateConfiguration(),
                "Same device for both players with same scheme should be invalid");
        }

        // === SplitKeyboardConfig Tests ===

        [Test]
        public void SplitKeyboardPlayerConfig_Player1Default_HasWASDMovement()
        {
            var config = SplitKeyboardPlayerConfig.CreatePlayer1Default();
            Assert.AreEqual("W", config.MoveUp);
            Assert.AreEqual("S", config.MoveDown);
            Assert.AreEqual("A", config.MoveLeft);
            Assert.AreEqual("D", config.MoveRight);
        }

        [Test]
        public void SplitKeyboardPlayerConfig_Player2Default_HasArrowMovement()
        {
            var config = SplitKeyboardPlayerConfig.CreatePlayer2Default();
            Assert.AreEqual("UpArrow", config.MoveUp);
            Assert.AreEqual("DownArrow", config.MoveDown);
            Assert.AreEqual("LeftArrow", config.MoveLeft);
            Assert.AreEqual("RightArrow", config.MoveRight);
        }

        [Test]
        public void SplitKeyboardPlayerConfig_Player1Default_HasActionKeys()
        {
            var config = SplitKeyboardPlayerConfig.CreatePlayer1Default();
            Assert.AreEqual("Space", config.Pass);
            Assert.AreEqual("F", config.Shoot);
            Assert.AreEqual("LeftShift", config.Sprint);
            Assert.AreEqual("Q", config.Switch);
            Assert.AreEqual("E", config.LobPass);
        }

        [Test]
        public void SplitKeyboardPlayerConfig_Player2Default_HasActionKeys()
        {
            var config = SplitKeyboardPlayerConfig.CreatePlayer2Default();
            Assert.AreEqual("Enter", config.Pass);
            Assert.AreEqual("RightShift", config.Shoot);
            Assert.AreEqual("RightControl", config.Sprint);
            Assert.AreEqual("Comma", config.Switch);
        }

        [Test]
        public void SplitKeyboardPlayerConfig_GetAllBoundKeys_ReturnsElevenKeys()
        {
            var config = SplitKeyboardPlayerConfig.CreatePlayer1Default();
            var keys = config.GetAllBoundKeys();
            Assert.AreEqual(11, keys.Length,
                "Should have 11 bound keys (4 movement + 7 action)");
        }

        [Test]
        public void SplitKeyboardPlayerConfig_UsesKey_TrueForBoundKey()
        {
            var config = SplitKeyboardPlayerConfig.CreatePlayer1Default();
            Assert.IsTrue(config.UsesKey("Space"),
                "Player 1 config should use Space key");
            Assert.IsTrue(config.UsesKey("space"),
                "Key matching should be case-insensitive");
        }

        [Test]
        public void SplitKeyboardPlayerConfig_UsesKey_FalseForUnboundKey()
        {
            var config = SplitKeyboardPlayerConfig.CreatePlayer1Default();
            Assert.IsFalse(config.UsesKey("Enter"),
                "Player 1 config should not use Enter key");
        }

        [Test]
        public void SplitKeyboardConfig_DefaultConfigs_NoConflict()
        {
            var config = new SplitKeyboardConfig();
            Assert.IsTrue(config.IsValid(),
                "Default P1 (WASD) and P2 (Arrows) should have no key conflicts");
        }

        [Test]
        public void SplitKeyboardConfig_GetOwningPlayer_ReturnsCorrectPlayer()
        {
            var config = new SplitKeyboardConfig();
            Assert.AreEqual(0, config.GetOwningPlayer("W"),
                "W should belong to Player 1");
            Assert.AreEqual(1, config.GetOwningPlayer("UpArrow"),
                "UpArrow should belong to Player 2");
            Assert.AreEqual(-1, config.GetOwningPlayer("Z"),
                "Z should belong to no player");
        }

        [Test]
        public void SplitKeyboardConfig_ResolveMovement_Player1WASD()
        {
            var config = new SplitKeyboardConfig();
            var (player, h, v) = config.ResolveMovement("W");
            Assert.AreEqual(0, player, "W should resolve to player 0");
            Assert.AreEqual(0f, h, "W should have horizontal 0");
            Assert.AreEqual(1f, v, "W should have vertical +1");

            (player, h, v) = config.ResolveMovement("A");
            Assert.AreEqual(0, player, "A should resolve to player 0");
            Assert.AreEqual(-1f, h, "A should have horizontal -1");
        }

        [Test]
        public void SplitKeyboardConfig_ResolveMovement_Player2Arrows()
        {
            var config = new SplitKeyboardConfig();
            var (player, h, v) = config.ResolveMovement("UpArrow");
            Assert.AreEqual(1, player, "UpArrow should resolve to player 1");
            Assert.AreEqual(0f, h, "UpArrow should have horizontal 0");
            Assert.AreEqual(1f, v, "UpArrow should have vertical +1");

            (player, h, v) = config.ResolveMovement("RightArrow");
            Assert.AreEqual(1, player, "RightArrow should resolve to player 1");
            Assert.AreEqual(1f, h, "RightArrow should have horizontal +1");
        }

        [Test]
        public void SplitKeyboardConfig_ResolveAction_Player1Pass()
        {
            var config = new SplitKeyboardConfig();
            var (player, action) = config.ResolveAction("Space");
            Assert.AreEqual(0, player, "Space should resolve to player 0");
            Assert.AreEqual(ActionType.Pass, action, "Space should be Pass action");
        }

        [Test]
        public void SplitKeyboardConfig_ResolveAction_Player2Pass()
        {
            var config = new SplitKeyboardConfig();
            var (player, action) = config.ResolveAction("Enter");
            Assert.AreEqual(1, player, "Enter should resolve to player 1");
            Assert.AreEqual(ActionType.Pass, action, "Enter should be Pass action");
        }

        [Test]
        public void SplitKeyboardConfig_ResolveAction_UnknownKey_ReturnsNone()
        {
            var config = new SplitKeyboardConfig();
            var (player, action) = config.ResolveAction("Z");
            Assert.AreEqual(-1, player);
            Assert.AreEqual(ActionType.None, action);
        }

        // === SplitKeyboardConflictChecker Tests ===

        [Test]
        public void SplitKeyboardConflictChecker_NoConflict_DefaultConfigs()
        {
            var checker = new SplitKeyboardConflictChecker();
            var p1 = SplitKeyboardPlayerConfig.CreatePlayer1Default();
            var p2 = SplitKeyboardPlayerConfig.CreatePlayer2Default();
            Assert.IsFalse(checker.HasConflict(p1, p2),
                "Default P1 and P2 configs should have no conflicts");
        }

        [Test]
        public void SplitKeyboardConflictChecker_Conflict_WhenSameKey()
        {
            var checker = new SplitKeyboardConflictChecker();
            var p1 = SplitKeyboardPlayerConfig.CreatePlayer1Default();
            var p2 = SplitKeyboardPlayerConfig.CreatePlayer2Default();
            p2.MoveUp = "W"; // Conflict with P1's MoveUp
            Assert.IsTrue(checker.HasConflict(p1, p2),
                "Should detect conflict when P2 uses P1's W key");
        }

        [Test]
        public void SplitKeyboardConflictChecker_GetConflictingKeys_ReturnsConflicts()
        {
            var checker = new SplitKeyboardConflictChecker();
            var p1 = SplitKeyboardPlayerConfig.CreatePlayer1Default();
            var p2 = SplitKeyboardPlayerConfig.CreatePlayer2Default();
            p2.MoveUp = "W";
            p2.Pass = "Space";

            var conflicts = checker.GetConflictingKeys(p1, p2);
            Assert.AreEqual(2, conflicts.Count,
                $"Should find 2 conflicts but found {conflicts.Count}: [{string.Join(", ", conflicts)}]");
            Assert.Contains("W", conflicts);
            Assert.Contains("Space", conflicts);
        }

        [Test]
        public void SplitKeyboardConflictChecker_NullConfigs_NoConflict()
        {
            var checker = new SplitKeyboardConflictChecker();
            Assert.IsFalse(checker.HasConflict(null, null));
            Assert.IsFalse(checker.HasConflict(
                SplitKeyboardPlayerConfig.CreatePlayer1Default(), null));
        }

        // === SplitControlConfig Tests ===

        [Test]
        public void SplitControlConfig_Default_IsKeyboardAndGamepad()
        {
            var config = new SplitControlConfig();
            Assert.AreEqual(SplitControlMode.KeyboardAndGamepad, config.Mode);
        }

        [Test]
        public void SplitControlConfig_CreateSplitKeyboard_HasCorrectMode()
        {
            var config = SplitControlConfig.CreateSplitKeyboard();
            Assert.AreEqual(SplitControlMode.SplitKeyboard, config.Mode);
        }

        [Test]
        public void SplitControlConfig_CreateDualGamepad_HasCorrectMode()
        {
            var config = SplitControlConfig.CreateDualGamepad();
            Assert.AreEqual(SplitControlMode.DualGamepad, config.Mode);
        }

        [Test]
        public void SplitControlConfig_HasPlayerKeyConfigs()
        {
            var config = new SplitControlConfig();
            Assert.IsNotNull(config.Player1Keys, "Player 1 key config should not be null");
            Assert.IsNotNull(config.Player2Keys, "Player 2 key config should not be null");
        }

        // === DeviceAssignment Tests ===

        [Test]
        public void DeviceAssignment_Constructor_SetsProperties()
        {
            var assignment = new DeviceAssignment("Keyboard", 1, 0);
            Assert.AreEqual("Keyboard", assignment.DeviceType);
            Assert.AreEqual(1, assignment.DeviceId);
            Assert.AreEqual(0, assignment.PlayerSlotIndex);
        }

        [Test]
        public void DeviceAssignment_Gamepad_SetsProperties()
        {
            var assignment = new DeviceAssignment("Gamepad", 42, 1);
            Assert.AreEqual("Gamepad", assignment.DeviceType);
            Assert.AreEqual(42, assignment.DeviceId);
            Assert.AreEqual(1, assignment.PlayerSlotIndex);
        }

        // === Integration / Cross-component Tests ===

        [Test]
        public void FullSetup_DefaultConfig_PassesAllValidation()
        {
            var config = new LocalMultiplayerConfig();
            var assigner = new ControlSchemeAssigner();
            var router = new DeviceInputRouter();
            var detector = new InputConflictDetector(router, assigner);
            var lobby = new LobbyLogic();

            // Simulate a full setup
            router.AssignDevice(0, 0);   // Keyboard -> P1
            router.AssignDevice(100, 1); // Gamepad -> P2
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);

            Assert.AreEqual(2, config.HumanPlayerCount, "Should have 2 human players");
            Assert.AreEqual(8, config.AIPlayerCount, "Should have 8 AI players");
            Assert.IsTrue(assigner.AreSchemesSeparate(), "Schemes should be separate");
            Assert.IsTrue(detector.ValidateConfiguration(), "Configuration should be valid");
            Assert.IsTrue(lobby.AreAllSlotsOccupied(), "All slots should be occupied");
        }

        [Test]
        public void FullSetup_ReadyAndStartCountdown_WorksEndToEnd()
        {
            var lobby = new LobbyLogic(2f);
            lobby.JoinSlot(0);
            lobby.JoinSlot(1);
            lobby.SetReady(0, true);
            lobby.SetReady(1, true);

            Assert.IsTrue(lobby.CanStartMatch(), "Should be startable");
            lobby.StartCountdown();
            Assert.AreEqual(LobbyState.CountingDown, lobby.State);

            // Tick halfway
            lobby.TickCountdown(1f);
            Assert.AreEqual(LobbyState.CountingDown, lobby.State,
                "Should still be counting down at 1s");
            Assert.That(lobby.CountdownRemaining, Is.EqualTo(1f).Within(0.01f));

            // Tick to completion
            bool started = false;
            lobby.OnMatchStart += () => started = true;
            lobby.TickCountdown(1.5f);
            Assert.IsTrue(started, "Match should have started");
            Assert.AreEqual(LobbyState.Starting, lobby.State);
        }
    }

    [TestFixture]
    [Category("US050")]
    [Category("Input")]
    public class US050_LocalMultiplayerHUDTests
    {
        [Test]
        public void GetJoinPrompt_KeyboardMouse_ReturnsSpacePrompt()
        {
            string prompt = OpenFifa.Gameplay.LocalMultiplayerHUD.GetJoinPrompt(ControlScheme.KeyboardMouse);
            Assert.That(prompt, Does.Contain("Space").IgnoreCase,
                "Keyboard join prompt should mention Space key");
        }

        [Test]
        public void GetJoinPrompt_Gamepad_ReturnsAButtonPrompt()
        {
            string prompt = OpenFifa.Gameplay.LocalMultiplayerHUD.GetJoinPrompt(ControlScheme.Gamepad);
            Assert.That(prompt, Does.Contain("A"),
                "Gamepad join prompt should mention A button");
        }

        [Test]
        public void GetReadyPrompt_KeyboardMouse_ReturnsEnterPrompt()
        {
            string prompt = OpenFifa.Gameplay.LocalMultiplayerHUD.GetReadyPrompt(ControlScheme.KeyboardMouse);
            Assert.That(prompt, Does.Contain("ENTER").IgnoreCase,
                "Keyboard ready prompt should mention Enter key");
        }

        [Test]
        public void GetReadyPrompt_Gamepad_ReturnsStartPrompt()
        {
            string prompt = OpenFifa.Gameplay.LocalMultiplayerHUD.GetReadyPrompt(ControlScheme.Gamepad);
            Assert.That(prompt, Does.Contain("START").IgnoreCase,
                "Gamepad ready prompt should mention Start button");
        }

        [Test]
        public void FormatSlotDisplay_OccupiedReady_ShowsReady()
        {
            var slot = new PlayerSlot(0, ControlScheme.KeyboardMouse, 0);
            slot.IsOccupied = true;
            slot.IsReady = true;
            string display = OpenFifa.Gameplay.LocalMultiplayerHUD.FormatSlotDisplay(slot);
            Assert.That(display, Does.Contain("READY"),
                "Occupied+ready slot should show READY");
            Assert.That(display, Does.Contain("Keyboard"),
                "Keyboard scheme should be shown");
        }

        [Test]
        public void FormatSlotDisplay_OccupiedNotReady_ShowsNotReady()
        {
            var slot = new PlayerSlot(1, ControlScheme.Gamepad, 1);
            slot.IsOccupied = true;
            slot.IsReady = false;
            string display = OpenFifa.Gameplay.LocalMultiplayerHUD.FormatSlotDisplay(slot);
            Assert.That(display, Does.Contain("Not Ready"),
                "Occupied+not-ready slot should show Not Ready");
            Assert.That(display, Does.Contain("Gamepad"),
                "Gamepad scheme should be shown");
        }

        [Test]
        public void FormatSlotDisplay_Empty_ShowsEmpty()
        {
            var slot = new PlayerSlot(0, ControlScheme.KeyboardMouse, 0);
            slot.IsOccupied = false;
            string display = OpenFifa.Gameplay.LocalMultiplayerHUD.FormatSlotDisplay(slot);
            Assert.That(display, Does.Contain("Empty"),
                "Unoccupied slot should show Empty");
        }

        [Test]
        public void FormatSlotDisplay_NullSlot_ReturnsEmpty()
        {
            string display = OpenFifa.Gameplay.LocalMultiplayerHUD.FormatSlotDisplay(null);
            Assert.AreEqual("", display, "Null slot should return empty string");
        }

        [Test]
        public void FormatCountdown_PositiveSeconds_ReturnsWholeNumber()
        {
            string display = OpenFifa.Gameplay.LocalMultiplayerHUD.FormatCountdown(2.7f);
            Assert.AreEqual("3", display, "2.7s should ceil to 3");
        }

        [Test]
        public void FormatCountdown_ZeroOrNegative_ReturnsGo()
        {
            Assert.AreEqual("GO!", OpenFifa.Gameplay.LocalMultiplayerHUD.FormatCountdown(0f));
            Assert.AreEqual("GO!", OpenFifa.Gameplay.LocalMultiplayerHUD.FormatCountdown(-1f));
        }
    }
}
