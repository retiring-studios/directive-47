using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

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

    private static StackPanel Row(SettingsPage page, string setting, string inForce)
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
            Text = inForce,
            FontSize = 20,
            MinWidth = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        // On losing focus rather than on every keystroke. "Ctrl+Alt" is a
        // prefix of a hotkey somebody is halfway through typing, and applying
        // it would claim a combination they did not ask for and then release it
        // again a keystroke later.
        field.LostFocus += (_, _) => page._changed(setting, field.Text);

        page._fields[setting] = field;

        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };

        row.Children.Add(label);
        row.Children.Add(field);

        return row;
    }
}
