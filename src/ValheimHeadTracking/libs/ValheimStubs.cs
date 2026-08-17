// Compile-time stubs for the Valheim types this mod binds against, built into
// an assembly_valheim.dll reference stub by scripts/setup-libs.ps1.
//
// Hand-written from the call sites in src/: only the members the mod actually
// uses are declared, and every body is empty. At runtime BepInEx loads the real
// assembly_valheim.dll, so these declarations exist purely to make the compiler
// emit the right member references.
//
// That last point is why shape matters more than it looks:
//   - `instance` is a PROPERTY on every one of these types. Declaring it as a
//     field emits `ldsfld`, which throws MissingFieldException against the real
//     assembly.
//   - `m_eye` is declared on Character, not Player. A field reference binds to
//     its declaring type, so the hierarchy has to be modelled.
//   - MessageHud.ShowMessage has five parameters with defaults. C# bakes the
//     defaults in at the call site, so a two-parameter stub emits a call to a
//     method that does not exist.
// Verified against the shipped assembly's public API. Adding a call to a new
// game member means adding its exact signature here.

using UnityEngine;
using UnityEngine.UI;

public class Character : MonoBehaviour
{
    public Transform m_eye;

    public virtual bool IsDead() => false;
    public virtual bool InCutscene() => false;
}

public class Humanoid : Character
{
}

public class Player : Humanoid
{
    public static Player m_localPlayer;

    public override bool IsDead() => false;
    public override bool InCutscene() => false;
}

public class GameCamera : MonoBehaviour
{
    public static GameCamera instance => null;
}

public class Hud : MonoBehaviour
{
    public static Hud instance => null;
    public static bool IsPieceSelectionVisible() => false;

    public Image m_crosshair;
    public Image m_crosshairBow;
    // The real field is a TMPro type. It is only ever read reflectively, by
    // name, so the declared type here just has to be a Component.
    public Text m_hoverName;
}

public class Game : MonoBehaviour
{
    public static Game instance => null;
}

public class Menu : MonoBehaviour
{
    public static Menu instance => null;
    public static bool IsVisible() => false;
}

public class InventoryGui : MonoBehaviour
{
    public static InventoryGui instance => null;
    public static bool IsVisible() => false;
}

public class TextInput : MonoBehaviour
{
    public static TextInput instance => null;
    public static bool IsVisible() => false;
}

public class Console : MonoBehaviour
{
    public static Console instance => null;
    public static bool IsVisible() => false;
}

public class MessageHud : MonoBehaviour
{
    public static MessageHud instance => null;

    public enum MessageType { TopLeft = 1, Center }

    public void ShowMessage(MessageType type, string text, int amount = 0,
                            Sprite icon = null, bool showDespiteHiddenHUD = false) { }
}
