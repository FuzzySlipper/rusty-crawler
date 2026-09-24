namespace PartyRpg.Host;

/// <summary>
/// The product's own identity, declared once in code and once in the project file. The architecture
/// suite fails when the two disagree, because a projection stream the shell never opens, or a product
/// id that does not match the staged metadata, is a defect that otherwise only shows up at runtime.
/// </summary>
internal static class ProductIdentity
{
    /// <summary>The product id declared to the engine.</summary>
    internal const string Id = "rusty-crawler-partyrpg";

    /// <summary>The product title declared to the engine.</summary>
    internal const string Title = "Rusty Crawler";

    /// <summary>The UI projection stream the product publishes.</summary>
    internal const string UiStream = "crawler.hud";

    /// <summary>The versioned projection contract carried on that stream.</summary>
    internal const string UiContract = "crawler.ui.snapshot.v1";

    /// <summary>The product-payload input intent the DOM companion claims actions on.</summary>
    internal const string UiActionIntent = "crawler.ui";

    /// <summary>The payload contract those actions carry.</summary>
    internal const string UiActionContract = "crawler.ui.action.v1";

    /// <summary>The digital intent that holds or releases the session from the keyboard.</summary>
    internal const string PauseToggleIntent = "session.pause-toggle";
}
