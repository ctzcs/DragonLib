using Foster.Framework;
using Prowl.PaperUI;

namespace Engine.Paper;

/// <summary>
/// 把 Foster 的鼠标 / 键盘输入转发给 <see cref="Paper"/>。
/// 每帧在 <c>paper.BeginFrame</c> 之前调用 <see cref="Update"/>。
/// </summary>
public static class PaperInput
{
	private static readonly (PaperKey Paper, Keys Foster)[] KeyMap =
	[
		(PaperKey.Tab, Keys.Tab),
		(PaperKey.Left, Keys.Left),
		(PaperKey.Right, Keys.Right),
		(PaperKey.Up, Keys.Up),
		(PaperKey.Down, Keys.Down),
		(PaperKey.PageUp, Keys.PageUp),
		(PaperKey.PageDown, Keys.PageDown),
		(PaperKey.Home, Keys.Home),
		(PaperKey.End, Keys.End),
		(PaperKey.Insert, Keys.Insert),
		(PaperKey.Delete, Keys.Delete),
		(PaperKey.Backspace, Keys.Backspace),
		(PaperKey.Space, Keys.Space),
		(PaperKey.Enter, Keys.Enter),
		(PaperKey.Escape, Keys.Escape),
		(PaperKey.LeftControl, Keys.LeftControl),
		(PaperKey.LeftShift, Keys.LeftShift),
		(PaperKey.LeftAlt, Keys.LeftAlt),
		(PaperKey.LeftSuper, Keys.LeftOS),
		(PaperKey.RightControl, Keys.RightControl),
		(PaperKey.RightShift, Keys.RightShift),
		(PaperKey.RightAlt, Keys.RightAlt),
		(PaperKey.RightSuper, Keys.RightOS),
		(PaperKey.Menu, Keys.Menu),
		(PaperKey.Application, Keys.Application),
		(PaperKey.Help, Keys.Help),
		(PaperKey.Select, Keys.Select),
		(PaperKey.Num0, Keys.D0),
		(PaperKey.Num1, Keys.D1),
		(PaperKey.Num2, Keys.D2),
		(PaperKey.Num3, Keys.D3),
		(PaperKey.Num4, Keys.D4),
		(PaperKey.Num5, Keys.D5),
		(PaperKey.Num6, Keys.D6),
		(PaperKey.Num7, Keys.D7),
		(PaperKey.Num8, Keys.D8),
		(PaperKey.Num9, Keys.D9),
		(PaperKey.A, Keys.A),
		(PaperKey.B, Keys.B),
		(PaperKey.C, Keys.C),
		(PaperKey.D, Keys.D),
		(PaperKey.E, Keys.E),
		(PaperKey.F, Keys.F),
		(PaperKey.G, Keys.G),
		(PaperKey.H, Keys.H),
		(PaperKey.I, Keys.I),
		(PaperKey.J, Keys.J),
		(PaperKey.K, Keys.K),
		(PaperKey.L, Keys.L),
		(PaperKey.M, Keys.M),
		(PaperKey.N, Keys.N),
		(PaperKey.O, Keys.O),
		(PaperKey.P, Keys.P),
		(PaperKey.Q, Keys.Q),
		(PaperKey.R, Keys.R),
		(PaperKey.S, Keys.S),
		(PaperKey.T, Keys.T),
		(PaperKey.U, Keys.U),
		(PaperKey.V, Keys.V),
		(PaperKey.W, Keys.W),
		(PaperKey.X, Keys.X),
		(PaperKey.Y, Keys.Y),
		(PaperKey.Z, Keys.Z),
		(PaperKey.F1, Keys.F1),
		(PaperKey.F2, Keys.F2),
		(PaperKey.F3, Keys.F3),
		(PaperKey.F4, Keys.F4),
		(PaperKey.F5, Keys.F5),
		(PaperKey.F6, Keys.F6),
		(PaperKey.F7, Keys.F7),
		(PaperKey.F8, Keys.F8),
		(PaperKey.F9, Keys.F9),
		(PaperKey.F10, Keys.F10),
		(PaperKey.F11, Keys.F11),
		(PaperKey.F12, Keys.F12),
		(PaperKey.Apostrophe, Keys.Apostrophe),
		(PaperKey.Comma, Keys.Comma),
		(PaperKey.Minus, Keys.Minus),
		(PaperKey.Period, Keys.Period),
		(PaperKey.Slash, Keys.Slash),
		(PaperKey.Semicolon, Keys.Semicolon),
		(PaperKey.Equals, Keys.Equals),
		(PaperKey.LeftBracket, Keys.LeftBracket),
		(PaperKey.Backslash, Keys.Backslash),
		(PaperKey.RightBracket, Keys.RightBracket),
		(PaperKey.Grave, Keys.Tilde),
		(PaperKey.CapsLock, Keys.Capslock),
		(PaperKey.ScrollLock, Keys.ScrollLock),
		(PaperKey.NumLock, Keys.Numlock),
		(PaperKey.PrintScreen, Keys.PrintScreen),
		(PaperKey.Pause, Keys.Pause),
		(PaperKey.Keypad0, Keys.Keypad0),
		(PaperKey.Keypad1, Keys.Keypad1),
		(PaperKey.Keypad2, Keys.Keypad2),
		(PaperKey.Keypad3, Keys.Keypad3),
		(PaperKey.Keypad4, Keys.Keypad4),
		(PaperKey.Keypad5, Keys.Keypad5),
		(PaperKey.Keypad6, Keys.Keypad6),
		(PaperKey.Keypad7, Keys.Keypad7),
		(PaperKey.Keypad8, Keys.Keypad8),
		(PaperKey.Keypad9, Keys.Keypad9),
		(PaperKey.KeypadDecimal, Keys.KeypadPeroid),
		(PaperKey.KeypadDivide, Keys.KeypadDivide),
		(PaperKey.KeypadMultiply, Keys.KeypadMultiply),
		(PaperKey.KeypadMinus, Keys.KeypadMinus),
		(PaperKey.KeypadPlus, Keys.KeypadPlus),
		(PaperKey.KeypadEnter, Keys.KeypadEnter),
		(PaperKey.KeypadEquals, Keys.KeypadEquals),
	];

	/// <summary>
	/// 转发本帧 Foster 输入到 Paper。
	/// </summary>
	/// <param name="paper">Paper 实例</param>
	/// <param name="app">Foster App（读 Input / Window）</param>
	/// <param name="scale">
	/// 与 UI 坐标缩放一致：若 Paper 分辨率用像素且与窗口一致，保持 1；
	/// 若 Paper 用逻辑分辨率（类似 ImGui 的 Scale），传入相同缩放。
	/// </param>
	public static void Update(Prowl.PaperUI.Paper paper, App app, float scale = 1f)
	{
		ArgumentNullException.ThrowIfNull(paper);
		ArgumentNullException.ThrowIfNull(app);
		if (scale <= 0f)
			scale = 1f;

		var input = app.Input;
		var mouse = input.Mouse;
		var keyboard = input.Keyboard;

		var x = mouse.Position.X / scale;
		var y = mouse.Position.Y / scale;

		paper.SetPointerPosition(x, y);

		if (mouse.Delta.X != 0 || mouse.Delta.Y != 0)
			paper.SetPointerState(PaperMouseBtn.Unknown, x, y, isPointerBtnDown: false, isPointerMove: true);

		ForwardMouseButton(paper, mouse, PaperMouseBtn.Left, MouseButtons.Left, x, y);
		ForwardMouseButton(paper, mouse, PaperMouseBtn.Right, MouseButtons.Right, x, y);
		ForwardMouseButton(paper, mouse, PaperMouseBtn.Middle, MouseButtons.Middle, x, y);

		if (mouse.Wheel.Y != 0)
			paper.SetPointerWheel(mouse.Wheel.Y);

		foreach (var (paperKey, fosterKey) in KeyMap)
		{
			if (keyboard.Pressed(fosterKey))
				paper.SetKeyState(paperKey, true);
			if (keyboard.Released(fosterKey))
				paper.SetKeyState(paperKey, false);
		}

		if (keyboard.Text.Length > 0)
		{
			for (var i = 0; i < keyboard.Text.Length; i++)
				paper.AddInputCharacter(keyboard.Text[i].ToString());
		}
	}

	private static void ForwardMouseButton(
		Prowl.PaperUI.Paper paper,
		MouseState mouse,
		PaperMouseBtn paperBtn,
		MouseButtons fosterBtn,
		float x,
		float y)
	{
		if (mouse.Pressed(fosterBtn))
			paper.SetPointerState(paperBtn, x, y, isPointerBtnDown: true, isPointerMove: false);
		if (mouse.Released(fosterBtn))
			paper.SetPointerState(paperBtn, x, y, isPointerBtnDown: false, isPointerMove: false);
	}
}
