using System.Text;
using PartyRpg.Kit.Presentation;
using Rusty.Engine;

namespace PartyRpg.Host;

/// <summary>
/// Reads which key the product's own declaration bound each control to, from the mappings the engine handed
/// the product at creation.
/// </summary>
/// <remarks>
/// <para>
/// <b>The project file is the one place a key is chosen.</b> Its <c>RustyEngineProductInputMapping</c> items are
/// what the engine admits, and the engine hands the product exactly those mappings when it creates it; reading
/// them here is what lets the panel say "save with the F key" about the key the engine will actually deliver,
/// rather than a letter a screen was written with. A control the declaration binds to no key reads as none, and
/// the panel then names its button alone.
/// </para>
/// <para>
/// Only keyboard mappings are read: a control a pointer or a controller presses has no key a hint could name.
/// </para>
/// </remarks>
internal static class ProductControlKeys
{
    /// <summary>The keys the engine's mappings bind the product's controls to.</summary>
    /// <param name="input">The input configuration the engine created the product with.</param>
    /// <returns>Each control's key, as a person reads it.</returns>
    internal static ControlKeys Read(ProductInputConfiguration input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ProductInputMapping[] mappings = input.PhysicalMappings.ToArray();
        string Key(string intent)
        {
            foreach (ProductInputMapping mapping in mappings)
            {
                if (mapping.TriggerKind != InputTriggerKind.Key) continue;
                if (!string.Equals(Encoding.UTF8.GetString(mapping.Intent.Span), intent, StringComparison.Ordinal)) continue;
                return ControlKeys.Label(mapping.Keyboard);
            }

            return string.Empty;
        }

        return new ControlKeys
        {
            Pause = Key(ProductIdentity.PauseToggleIntent),
            Save = Key(ProductIdentity.SaveIntent),
            Use = Key(ProductIdentity.UseIntent),
            Attack = Key(ProductIdentity.AttackIntent),
            NextMember = Key(ProductIdentity.NextMemberIntent),
            TurnBased = Key(ProductIdentity.TurnBasedToggleIntent),
            TurnSkip = Key(ProductIdentity.TurnSkipIntent),
            TurnWait = Key(ProductIdentity.TurnWaitIntent),
            Rest = Key(ProductIdentity.RestIntent),
            Camp = Key(ProductIdentity.CampIntent),
            WaitDawn = Key(ProductIdentity.WaitUntilDawnIntent),
            WaitHour = Key(ProductIdentity.WaitAnHourIntent),
            WaitFiveMinutes = Key(ProductIdentity.WaitFiveMinutesIntent),
            ServiceLeave = Key(ProductIdentity.ServiceLeaveIntent),
            ConversationLeave = Key(ProductIdentity.ConversationLeaveIntent),
            CreationAdvance = Key(ProductIdentity.CreationAdvanceIntent),
            CreationAccept = Key(ProductIdentity.CreationAcceptIntent),
        };
    }
}
