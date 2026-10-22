using UnityEngine;
using UnityEngine.InputSystem;

public static class PointerUtil
{
    public static bool WasPressedThisFrame()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    public static bool WasReleasedThisFrame()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            return true;
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    }

    public static bool IsPressed()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    public static Vector2 ScreenPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return Vector2.zero;
    }

    public static Vector3 WorldOnPlane(Camera camera)
    {
        Vector3 screen = ScreenPosition();
        screen.z = -camera.transform.position.z;
        return camera.ScreenToWorldPoint(screen);
    }
}
