using System;
using System.Collections.Generic;

namespace Refine.App.Models;

public enum PopupMode
{
    SingleAction,
    Confirmation
}

public enum PopupStyle
{
    Standard,
    Large
}

public enum PopupButtonType
{
    Primary,
    Secondary,
    Ghost,
    Destructive
}

public class PopupButton
{
    public string Text { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public PopupButtonType Type { get; set; } = PopupButtonType.Secondary;
    public string Icon { get; set; } = string.Empty;
}

public class PopupParameter
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class PopupResult
{
    public const string ConfirmedAction = "Confirm";
    public const string RejectedAction = "Reject";
    public const string DismissedAction = "Dismiss";

    public string ActionId { get; set; } = string.Empty;

    public bool IsConfirmed => ActionId == ConfirmedAction;
    public bool IsRejected => ActionId == RejectedAction;
    public bool IsDismissed => ActionId == DismissedAction;

    public PopupResult(string actionId)
    {
        ActionId = actionId;
    }

    public static PopupResult Confirmed() => new(ConfirmedAction);
    public static PopupResult Rejected() => new(RejectedAction);
    public static PopupResult Dismissed() => new(DismissedAction);
    public static PopupResult Custom(string actionId) => new(actionId);
}

public class PopupOptions
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty; // Material symbol icon name
    
    public PopupMode Mode { get; set; } = PopupMode.Confirmation;
    public PopupStyle Style { get; set; } = PopupStyle.Standard;

    public string ConfirmText { get; set; } = "Confirm";
    public string RejectText { get; set; } = "Cancel";

    // Dynamic key-value pairs
    public List<PopupParameter> Parameters { get; set; } = new();

    // Custom buttons to override the default SingleAction / Confirmation behavior
    public List<PopupButton> CustomButtons { get; set; } = new();
}
