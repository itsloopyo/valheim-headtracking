using System.Runtime.CompilerServices;
using UnityEngine;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Determines when head tracking should be active based on the current game state.
    /// Optimized with early exits ordered by likelihood and check cost.
    /// </summary>
    public static class GameStateDetector
    {
        // Cached camera reference to avoid GetComponent in hot path
        private static Camera _cachedCamera;
        private static GameCamera _cachedGameCamera;

        /// <summary>
        /// Checks all game state conditions to determine if head tracking should be applied.
        /// Early exits are ordered by: (1) likelihood of failure, (2) check cost.
        /// </summary>
        /// <returns>True if head tracking should be applied, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldTrack()
        {
            // 1. Most likely to fail: no local player (loading, character select, etc.)
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }

            // 2. Game instance check (very cheap null check)
            if (Game.instance == null)
            {
                return false;
            }

            // 3. Common UI states - checked frequently during gameplay
            if (Menu.IsVisible())
            {
                return false;
            }

            if (InventoryGui.IsVisible())
            {
                return false;
            }

            if (TextInput.IsVisible())
            {
                return false;
            }

            if (Hud.IsPieceSelectionVisible())
            {
                return false;
            }

            // 4. Player state checks
            if (player.IsDead())
            {
                return false;
            }

            // 5. Cutscene handling
            if (!IsCutsceneWithCameraControl(player))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines if head tracking should be active during a cutscene.
        /// Uses cached Camera reference to avoid GetComponent overhead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCutsceneWithCameraControl(Player player)
        {
            // If not in a cutscene, camera control is allowed
            if (!player.InCutscene())
            {
                return true;
            }

            // During cutscenes, check if GameCamera is available
            var gameCamera = GameCamera.instance;
            if (gameCamera == null)
            {
                _cachedCamera = null;
                _cachedGameCamera = null;
                return false;
            }

            // Cache Camera component when GameCamera instance changes
            if (_cachedGameCamera != gameCamera)
            {
                _cachedCamera = gameCamera.GetComponent<Camera>();
                _cachedGameCamera = gameCamera;
            }

            if (_cachedCamera == null || !_cachedCamera.enabled)
            {
                return false;
            }

            return true;
        }
    }
}
