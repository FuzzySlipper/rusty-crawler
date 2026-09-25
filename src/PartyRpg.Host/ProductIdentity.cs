using PartyRpg.Kit.Sessions;

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

    /// <summary>The product title used in messages to the operator.</summary>
    internal const string ProductTitle = "Rusty Crawler";

    /// <summary>The directory inside the content root that this product's packs and bundles live in.</summary>
    internal const string ContentDirectory = "partyrpg";

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

    /// <summary>The digital intent that walks the party forward.</summary>
    internal const string MoveForwardIntent = "party.move-forward";

    /// <summary>The digital intent that walks the party backward.</summary>
    internal const string MoveBackIntent = "party.move-back";

    /// <summary>The digital intent that strafes the party to its left.</summary>
    internal const string StrafeLeftIntent = "party.strafe-left";

    /// <summary>The digital intent that strafes the party to its right.</summary>
    internal const string StrafeRightIntent = "party.strafe-right";

    /// <summary>The digital intent that turns the party to its left.</summary>
    internal const string TurnLeftIntent = "party.turn-left";

    /// <summary>The digital intent that turns the party to its right.</summary>
    internal const string TurnRightIntent = "party.turn-right";

    /// <summary>The digital intent that jumps the party.</summary>
    internal const string JumpIntent = "party.jump";

    /// <summary>The digital intent that confirms the creation step being worked on.</summary>
    internal const string CreationAdvanceIntent = "creation.advance";

    /// <summary>The digital intent that accepts the finished party and leaves creation for the world.</summary>
    internal const string CreationAcceptIntent = "creation.accept";

    /// <summary>The digital intent that asks the live session to save where it stands.</summary>
    internal const string SaveIntent = "session.save";

    /// <summary>The digital intent that uses whatever the party is facing.</summary>
    /// <remarks>
    /// The original's keyboard interaction key is Space and its jump key is X
    /// (<c>src/Application/GameConfig.h:544-548</c>, <c>event_trigger</c> and <c>jump</c>); this product
    /// already gives Space to jumping, so the use control takes G, which no other control claims during
    /// play. The name is declared in code and again in the project file, because the engine admits an event
    /// only on an intent its manifest carries and refuses to start on a mapping whose intent it does not.
    /// </remarks>
    internal const string UseIntent = "party.use";

    /// <summary>
    /// The payload action name that uses whatever the party is facing, sent by the DOM companion's use
    /// control on the UI action contract.
    /// </summary>
    internal const string UseAction = "party.use";

    /// <summary>
    /// The digital intent that leaves the service counter a visit has open.
    /// </summary>
    /// <remarks>
    /// Entering a service is deliberately not a control of its own: the party enters by using the person the
    /// interaction mechanism reached, on the use control above, so there is one way to walk up to somebody.
    /// Leaving is its own control — a player at a counter needs a way out that is not walking — and the
    /// counter's commands are payload actions on the contract the companion already claims. The original's
    /// interaction key is Space and its jump key is X; Space already jumps here, so leaving takes X, which no
    /// other control claims during play.
    /// </remarks>
    internal const string ServiceLeaveIntent = "service.leave";

    /// <summary>
    /// The digital intent that rests and heals where the party stands.
    /// </summary>
    /// <remarks>
    /// The original's rest key is R (<c>docs/research/mm7-manual-outline.md</c> p.59, "R to rest"), and this
    /// product keeps it: resting is the one stop the original gives a key of its own. The other stops take
    /// letters the original spends on screens this build does not have yet — T and H open two of its books
    /// and C casts — so a stone that adds books or casting moves these controls deliberately rather than
    /// inheriting a collision. Every stop is its own control because every stop is a different act: one key
    /// cannot mean "rest until healed" and "wait without healing". The declaration here and the mapping in
    /// the project file are the two halves of one control, because the engine refuses a mapping whose intent
    /// it was never told about.
    /// </remarks>
    internal const string RestIntent = RestActions.Rest;

    /// <summary>The digital intent that makes camp in the open, on the key the original would spend casting.</summary>
    internal const string CampIntent = RestActions.Camp;

    /// <summary>The digital intent that waits until the next dawn, on a key the original gives to a book.</summary>
    internal const string WaitUntilDawnIntent = RestActions.WaitUntilDawn;

    /// <summary>The digital intent that waits an hour, on a key the original gives to a book.</summary>
    internal const string WaitAnHourIntent = RestActions.WaitAnHour;

    /// <summary>
    /// The digital intent that waits a short interval — the manual's own "Wait 5 minutes" — on the key the
    /// original gives to its map book.
    /// </summary>
    internal const string WaitFiveMinutesIntent = RestActions.WaitFiveMinutes;

    /// <summary>
    /// The payload action name that asks the live session to save, sent by the DOM companion's save control
    /// on the UI action contract.
    /// </summary>
    internal const string SaveAction = "session.save";

    /// <summary>
    /// The environment variable that selects how a product run begins: a fresh session, or the one the save
    /// slot already holds.
    /// </summary>
    /// <remarks>
    /// The dev runner owns its command line and passes no arguments to the product, so an operator's choice
    /// between a new game and a resumed one arrives as an environment variable. It is read once, where the
    /// product is created, and reported in the projection: a switch that could only be inferred would leave
    /// an operator unable to tell a resumed session from a new one.
    /// </remarks>
    internal const string StartVariable = "RUSTY_CRAWLER_START";
}
