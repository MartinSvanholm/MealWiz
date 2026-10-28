using Microsoft.AspNetCore.Components.Web;

namespace MealWiz.Shared.Components.Form;

/// <summary>
/// OnKeyDown handlers for form fields. Enter (also the Enter/Next key of a phone keyboard) moves to the next field,
/// and on a form's last field submits it, e.g. <c>OnKeyDown="EnterKey.Then(() => _amount.FocusAsync())"</c>.
/// </summary>
public static class EnterKey
{
    public static Func<KeyboardEventArgs, Task> Then(Func<Task> action) =>
        args => IsEnter(args) ? action() : Task.CompletedTask;

    public static Func<KeyboardEventArgs, Task> Then(Func<ValueTask> action) =>
        args => IsEnter(args) ? action().AsTask() : Task.CompletedTask;

    private static bool IsEnter(KeyboardEventArgs args) => args.Key is "Enter" or "NumpadEnter";
}
