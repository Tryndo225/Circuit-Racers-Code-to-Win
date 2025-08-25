using UnityEngine;

public class ReadOnlyAttribute : PropertyAttribute
{ }

public class ShowIfAttribute : PropertyAttribute
{
    public readonly string Field;
    public readonly bool RequiredState;

    public ShowIfAttribute(string boolField)
    {
        Field = boolField;
        RequiredState = true;
    }

    public ShowIfAttribute(string boolField, bool mustBeTrue)
    {
        Field = boolField;
        RequiredState = mustBeTrue;
    }
}