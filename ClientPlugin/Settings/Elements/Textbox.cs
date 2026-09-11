using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ClientPlugin.Settings.Elements;

internal class TextboxAttribute : Attribute, IElement
{
    public readonly string Label;
    public readonly string Description;
    public readonly bool ReadOnly;

    // Subscription of the read-only box currently on screen, kept so the next
    // one can drop it; see MakeReadOnly.
    private PropertyChangedEventHandler refresh;

    public TextboxAttribute(string label = null, string description = null, bool readOnly = false)
    {
        Label = label;
        Description = description;
        ReadOnly = readOnly;
    }

    public List<Control> GetControls(string name, Func<object> propertyGetter, Action<object> propertySetter)
    {
        var initial = propertyGetter();
        var isInteger = initial is int;
        var value = initial.ToString();
        var textBox = new MyGuiControlTextbox(defaultText: value,
            type: isInteger ? MyGuiControlTextboxType.DigitsOnly : MyGuiControlTextboxType.Normal);

        if (ReadOnly)
            MakeReadOnly(textBox, propertyGetter, value);
        else if (isInteger)
            textBox.TextChanged += box => SetInteger(box.Text, propertySetter);
        else
            textBox.TextChanged += box => propertySetter(box.Text);

        textBox.SetToolTip(Description);

        var label = Tools.Tools.GetLabelOrDefault(name, Label);
        return new List<Control>()
        {
            new Control(new MyGuiControlLabel(text: label), minWidth: Control.LabelMinWidth),
            new Control(textBox, fillFactor: 1f),
        };
    }

    // MyGuiControlTextbox has no read-only mode and a disabled one cannot be
    // selected, so keep the box live (the value stays selectable and copyable)
    // and undo every edit instead. Assigning Text raises TextChanged again,
    // hence the guard.
    //
    // A read-only box shows a derived value, which can change while the dialog
    // is open because another setting it is derived from was edited (the
    // webhook URL follows the port). Controls are built anew each time the
    // dialog opens but this attribute instance outlives them, so the previous
    // box's subscription is dropped here rather than left to update a control
    // that is no longer on screen.
    private void MakeReadOnly(MyGuiControlTextbox textBox, Func<object> propertyGetter, string value)
    {
        var current = value;
        var reverting = false;

        textBox.TextChanged += box =>
        {
            if (reverting || box.Text == current)
                return;

            reverting = true;
            box.Text = current;
            reverting = false;
        };

        if (refresh != null)
            Config.Current.PropertyChanged -= refresh;

        refresh = (_, __) =>
        {
            var latest = propertyGetter()?.ToString() ?? "";
            if (latest == current)
                return;

            // Ahead of the assignment: the revert handler runs off it and has
            // to see the new value as the one to keep.
            current = latest;
            textBox.Text = latest;
        };

        Config.Current.PropertyChanged += refresh;
    }

    // Half-typed numbers are simply not applied, the property keeps its last value
    private static void SetInteger(string text, Action<object> propertySetter)
    {
        if (int.TryParse(text, out var number))
            propertySetter(number);
    }

    public List<Type> SupportedTypes { get; } = new List<Type>()
    {
        typeof(string),
        typeof(int)
    };
}
