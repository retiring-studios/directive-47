using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using D47.Data;
using D47.Render;

namespace D47.Panel;

/// <summary>
/// Where a Commander changes a setting, and sees what it is now.
///
/// <para>
/// Built from <see cref="Settings.Known"/> rather than from three rows typed
/// here, so a setting added to the schema and not to this page is a failing
/// test rather than a setting nobody can reach.
/// </para>
///
/// <para>
/// It reports rather than writes. What a changed setting means to the running
/// application — a panel that redraws, an overlay that goes more see-through, a
/// hotkey released and retaken — is knowledge the composition root already has
/// and this page has no business acquiring. A page that wrote the file itself
/// would leave everything holding the old value until the next launch, which is
/// the failure the criterion is named after.
/// </para>
///
/// <para>
/// Hand-built rather than XAML because the rows come from a list. A page that
/// declared three of them would be the second place the schema is stated, and
/// there is exactly one place on purpose.
/// </para>
/// </summary>
internal sealed class SettingsPage : StackPanel
{
    private readonly Dictionary<string, TextBox> _fields = [];
    private readonly Action<string, string> _changed;

    private SettingsPage(Action<string, string> changed)
    {
        _changed = changed;
        Margin = new Thickness(24);
        Background = Palette.Background;
    }

    /// <summary>
    /// What a setting is called where a Commander reads it.
    ///
    /// <para>
    /// Derived from the key rather than kept beside it. The keys were written to
    /// be said aloud — "overlay hotkey", "panel zoom" — which is the same
    /// requirement a label has, so a second list of names would be a second
    /// thing to keep in step for no gain.
    /// </para>
    /// </summary>
    /// <param name="setting">The setting's key.</param>
    /// <returns>The label.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="setting"/> is null.
    /// </exception>
    internal static string LabelFor(string setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return setting.Length == 0
            ? setting
            : char.ToUpper(setting[0], CultureInfo.InvariantCulture) + setting[1..];
    }

    /// <summary>
    /// Builds the page against what is in force right now.
    /// </summary>
    /// <param name="settings">What the Commander has chosen so far.</param>
    /// <param name="changed">Told which setting changed, and to what.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    internal static SettingsPage For(SettingsStore settings, Action<string, string> changed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(changed);

        var page = new SettingsPage(changed);

        foreach (string setting in Settings.Known)
        {
            page.Children.Add(Row(page, setting, settings.Read(setting) ?? string.Empty));
        }

        return page;
    }

    /// <summary>
    /// Changes a setting as though the Commander had, which is what the page's
    /// own controls do when they lose focus.
    /// </summary>
    /// <param name="setting">The setting's key.</param>
    /// <param name="value">What to change it to.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="setting"/> is null.
    /// </exception>
    internal void Change(string setting, string value)
    {
        ArgumentNullException.ThrowIfNull(setting);

        if (_fields.TryGetValue(setting, out TextBox? field))
        {
            field.Text = value;
        }

        _changed(setting, value);
    }

    /// <summary>
    /// Takes a combination the way the Commander pressed it.
    /// </summary>
    ///
    /// <remarks>
    /// On the key going down rather than on losing focus, which is how the rest
    /// of the page works. A combination has no half-way state: it is complete
    /// the moment a key that is not a modifier goes down with whatever
    /// modifiers are being held.
    ///
    /// <para>
    /// A modifier on its own is not one. Holding <c>Ctrl</c> on the way to
    /// <c>Ctrl</c>+<c>Alt</c>+<c>E</c> would otherwise claim <c>Ctrl</c>, and
    /// every combination is reached through its own modifiers going down first.
    /// </para>
    /// </remarks>
    /// <param name="held">Whatever modifiers were down.</param>
    /// <param name="pressed">The key that went down.</param>
    internal void Pressed(ModifierKeys held, Key pressed)
    {
        if (IsAModifier(pressed))
        {
            return;
        }

        Change(ChosenHotkey.WhatItIsCalled, new Combination(held, pressed).ToString());
    }

    /// <summary>
    /// The key itself, past the system-key wrapper WPF puts an Alt combination
    /// in.
    /// </summary>
    /// <param name="pressed">What arrived.</param>
    /// <returns>The key that was really pressed.</returns>
    private static Key WhatWasActuallyPressed(KeyEventArgs pressed) =>
        pressed.Key == Key.System ? pressed.SystemKey : pressed.Key;

    private static bool IsAModifier(Key pressed) => pressed
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin;

    private static StackPanel Row(SettingsPage page, string setting, string chosen)
    {
        var label = new TextBlock
        {
            Text = LabelFor(setting),
            Foreground = Palette.BodyText,
            FontSize = 20,
            Margin = new Thickness(0, 0, 0, 4),
        };

        var field = new TextBox
        {
            Text = chosen,
            FontSize = 20,
            MinWidth = 240,
            Padding = new Thickness(6, 4, 6, 4),
            Background = Palette.Background,
            Foreground = Palette.BodyText,
            CaretBrush = Palette.BodyText,
            BorderBrush = Palette.BodyText,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        // The default, behind an empty box rather than in it. In it, a
        // Commander who tabs past a row they never meant to touch commits the
        // shipped answer as an override, and the two layers become one.
        var shipped = new TextBlock
        {
            Text = Settings.DefaultFor(setting),
            FontSize = 20,
            Margin = new Thickness(7, 4, 0, 0),
            Opacity = 0.45,
            Foreground = Palette.BodyText,
            IsHitTestVisible = false,
            Visibility =
                chosen.Length == 0 ? Visibility.Visible : Visibility.Collapsed,
        };

        field.TextChanged += (_, _) => shipped.Visibility =
            field.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (setting == ChosenHotkey.WhatItIsCalled)
        {
            // Captured, not typed. The alternative is teaching a parser every
            // spelling a Commander might try — ctrl, control, Ctrl, CTRL, and
            // the same again for every modifier — when the only speller that
            // has to agree with the file is the one that writes it.
            field.IsReadOnly = true;
            field.PreviewKeyDown += (_, pressed) =>
            {
                pressed.Handled = true;
                page.Pressed(Keyboard.Modifiers, WhatWasActuallyPressed(pressed));
            };
        }
        else
        {
            // On losing focus rather than on every keystroke. A half-typed
            // number is a number, and applying it would move the panel to a
            // zoom nobody asked for on the way to the one they did.
            field.LostFocus += (_, _) => page._changed(setting, field.Text);
        }

        page._fields[setting] = field;

        var box = new Grid { HorizontalAlignment = HorizontalAlignment.Left };

        box.Children.Add(field);
        box.Children.Add(shipped);

        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };

        row.Children.Add(label);
        row.Children.Add(box);

        return row;
    }
}
