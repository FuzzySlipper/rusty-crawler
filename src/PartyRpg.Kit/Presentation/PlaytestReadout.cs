using System.Text;
using System.Text.Json;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// The session's gameplay facts as a playtest harness reads them, and whether the movement controls would step
/// the party now.
/// </summary>
/// <remarks>
/// <para>
/// <b>One reading, never a second opinion.</b> Every fact here is copied from the same snapshot the panel's
/// projection is built from, so a harness that asks where the party stands, what it faces, and what is fighting
/// it reads exactly what the panel would show, without scraping the panel to get it. Nothing is worked out here
/// that a block does not already state.
/// </para>
/// <para>
/// <b>Steering is the session's rule, stated over the snapshot.</b> The party's movement keys step it only while
/// the session runs, holds a world, and no screen or awaited turn holds the controls: that is when the session
/// hands what the player holds to the world. <see cref="Steering"/> states that rule once, for the harness that
/// asks whether a movement key would do anything and for the look that turns the party between updates.
/// </para>
/// </remarks>
public static class PlaytestReadout
{
    /// <summary>
    /// Why the session's movement controls would not step the party now, or null when they would.
    /// </summary>
    /// <param name="snapshot">The session as its projection reads it.</param>
    /// <returns>The refusal, or null when a held movement key would move the party.</returns>
    public static Refusal? Steering(SessionSnapshot snapshot) => snapshot.Mode switch
    {
        SessionMode.Creating => new Refusal(
            PlaytestCodes.SteerCreating,
            "A party is still being made: the party walks once creation is accepted."),
        SessionMode.Paused => new Refusal(
            PlaytestCodes.SteerHeld,
            "The session is held, so nothing moves until it is resumed."),
        SessionMode.TurnBased => new Refusal(
            PlaytestCodes.SteerTurn,
            "A paced fight is waiting for one of the party's turns: act, skip, or wait to pass it."),
        SessionMode.Running when !snapshot.World.HasWorld => new Refusal(
            PlaytestCodes.SteerNoWorld,
            "The session holds no places, so there is nowhere to walk."),
        SessionMode.Running when snapshot.Service.Open => new Refusal(
            PlaytestCodes.SteerScreen,
            "A counter is open and holds the controls: leave it first."),
        SessionMode.Running when snapshot.Conversation.Open => new Refusal(
            PlaytestCodes.SteerScreen,
            "A conversation is open and holds the controls: leave it first."),
        SessionMode.Running => null,
        _ => new Refusal(
            PlaytestCodes.SteerNotRunning,
            $"The session is {SessionProjection.WireName(snapshot.Mode)}, so nothing it holds is stepped."),
    };

    /// <summary>
    /// Why the session's controls to rise and sink would not move the party now, or null when they would.
    /// </summary>
    /// <remarks>
    /// They are movement controls first, so whatever stops a walk stops them; past that they act only while the party
    /// may fly, which is the movement owner's own answer and not a guess from what the party carries.
    /// </remarks>
    /// <param name="snapshot">The session as its projection reads it.</param>
    /// <returns>The refusal, or null when a held rise or sink would move the party.</returns>
    public static Refusal? Rising(SessionSnapshot snapshot) =>
        Steering(snapshot) ?? (snapshot.Movement.Flight
            ? null
            : new Refusal(
                PlaytestCodes.SteerNoFlight,
                "The party may not fly here now, so rising and sinking ask for nothing: it walks."));

    /// <summary>Writes the session's gameplay facts as one compact JSON object.</summary>
    /// <param name="snapshot">The session as its projection reads it.</param>
    /// <returns>The observation, as JSON text.</returns>
    public static string Observe(SessionSnapshot snapshot)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("mode", SessionProjection.WireName(snapshot.Mode));
            writer.WriteNumber("admittedSteps", snapshot.AdmittedSteps);
            writer.WriteNumber("simulationSeconds", snapshot.SimulationSeconds);

            // What the session was composed from — the bundle, how many packs it selected, and which start the party
            // took — so a live check reads which game it is playing from the session rather than from the panel.
            writer.WriteStartObject("composition");
            writer.WriteString("bundle", snapshot.Composition.Bundle ?? string.Empty);
            writer.WriteNumber("contentPacks", snapshot.Composition.ContentPacks);
            writer.WriteString("partyStart", SessionProjection.WireName(snapshot.Composition.PartyStart));
            writer.WriteEndObject();

            Refusal? steering = Steering(snapshot);
            writer.WriteStartObject("steering");
            writer.WriteBoolean("available", steering is null);
            writer.WriteString("code", steering?.Code ?? string.Empty);
            writer.WriteString("reason", steering?.Message ?? string.Empty);
            writer.WriteEndObject();

            WriteWorld(writer, snapshot.World);
            WriteMovement(writer, snapshot.Movement);
            WriteFacing(writer, snapshot.Interaction);
            WriteCombat(writer, snapshot.Combat);

            writer.WriteStartObject("party");
            writer.WriteBoolean("present", snapshot.Party.Present);
            writer.WriteNumber("members", snapshot.Party.Members);
            writer.WriteNumber("hitPoints", snapshot.Party.HitPoints);
            writer.WriteNumber("hitPointsMax", snapshot.Party.HitPointsMax);
            writer.WriteString("conditions", snapshot.Party.Conditions);
            writer.WriteEndObject();

            writer.WriteStartObject("screens");
            writer.WriteBoolean("creation", snapshot.Creation is { Active: true });
            writer.WriteBoolean("service", snapshot.Service.Open);
            writer.WriteBoolean("conversation", snapshot.Conversation.Open);
            writer.WriteEndObject();

            writer.WritePropertyName("clock");
            if (snapshot.Clock.Present)
            {
                writer.WriteStartObject();
                writer.WriteString("date", snapshot.Clock.Date);
                writer.WriteString("time", snapshot.Clock.Time);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNullValue();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteWorld(Utf8JsonWriter writer, WorldSnapshot world)
    {
        writer.WritePropertyName("place");
        if (!world.HasWorld)
        {
            writer.WriteNullValue();
            writer.WriteNull("pose");
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("id", world.Place);
        writer.WriteString("name", world.Name);
        writer.WriteString("kind", world.Kind);
        writer.WriteBoolean("open", world.Open);
        writer.WriteEndObject();

        writer.WriteStartObject("pose");
        writer.WriteNumber("x", world.Pose.X);
        writer.WriteNumber("y", world.Pose.Y);
        writer.WriteNumber("z", world.Pose.Z);
        writer.WriteNumber("yaw", world.Pose.Yaw);
        writer.WriteNumber("pitch", world.Pose.Pitch);
        writer.WriteString(
            "convention",
            "the party's feet in the place's own coordinates; yaw and pitch in the place's facing units, yaw growing as the party turns left");
        writer.WriteEndObject();
    }

    private static void WriteMovement(Utf8JsonWriter writer, MovementSnapshot movement)
    {
        writer.WriteStartObject("movement");
        writer.WriteBoolean("moved", movement.Moved);
        writer.WriteBoolean("grounded", movement.Grounded);
        writer.WriteString("blocked", SessionProjection.WireName(movement.Blocked));
        writer.WriteNumber("stepRise", movement.StepRise);
        writer.WriteNumber("fallDistance", movement.FallDistance);
        writer.WriteEndObject();
    }

    private static void WriteFacing(Utf8JsonWriter writer, InteractionSnapshot facing)
    {
        writer.WriteStartObject("facing");
        writer.WriteBoolean("available", facing.Available);
        writer.WriteString("target", facing.Target);
        writer.WriteString("label", facing.Label);
        writer.WriteString("disposition", facing.Disposition);
        writer.WriteString("verb", facing.Verb);
        writer.WriteString("state", facing.State);
        writer.WriteNumber("distance", facing.Distance);
        writer.WriteString("reason", facing.Reason);
        writer.WriteStartArray("requires");
        foreach (string requirement in facing.Requires ?? []) writer.WriteStringValue(requirement);
        writer.WriteEndArray();
        writer.WriteStartObject("lastUse");
        writer.WriteString("outcome", facing.Outcome);
        writer.WriteString("code", facing.Code);
        writer.WriteString("message", facing.Message);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteCombat(Utf8JsonWriter writer, CombatSnapshot combat)
    {
        writer.WriteStartObject("combat");
        writer.WriteBoolean("available", combat.Available);
        writer.WriteBoolean("engaged", combat.Engaged);
        writer.WriteString("pacing", SessionProjection.WireName(combat.Pacing));
        writer.WriteNumber("ready", combat.Ready);
        writer.WriteStartArray("hostile");
        foreach (CombatActorSnapshot enemy in combat.Enemies ?? []) WriteActor(writer, enemy);
        writer.WriteEndArray();
        writer.WriteStartArray("members");
        foreach (CombatActorSnapshot member in combat.Members ?? []) WriteActor(writer, member);
        writer.WriteEndArray();
        writer.WritePropertyName("turn");
        if (combat.Turn is { } turn)
        {
            writer.WriteStartObject();
            writer.WriteString("phase", SessionProjection.WireName(turn.Phase));
            writer.WriteNumber("round", turn.Round);
            writer.WriteString("actor", turn.Actor);
            writer.WriteBoolean("playerTurn", turn.PlayerTurn);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }

    private static void WriteActor(Utf8JsonWriter writer, CombatActorSnapshot actor)
    {
        writer.WriteStartObject();
        writer.WriteString("id", actor.Id);
        writer.WriteString("name", actor.Name);
        writer.WriteNumber("distance", actor.Distance);
        writer.WriteNumber("hitPoints", actor.HitPoints);
        writer.WriteNumber("hitPointsMax", actor.HitPointsMax);
        writer.WriteBoolean("ready", actor.Ready);
        writer.WriteBoolean("down", actor.Down);
        writer.WriteString("activity", actor.Activity);
        writer.WritePropertyName("pose");
        if (actor.Pose is { } pose)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", pose.X);
            writer.WriteNumber("y", pose.Y);
            writer.WriteNumber("z", pose.Z);
            writer.WriteEndObject();
        }
        else writer.WriteNullValue();
        writer.WriteEndObject();
    }
}
