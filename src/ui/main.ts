/**
 * The product's DOM companion: it renders the session projection and reports semantic actions.
 *
 * It owns no state, evaluates no rules, and starts no loop or timer. Every value it shows arrived in
 * the last projection from the product, and every action it sends is an action name the product
 * defines. When the projection says the session is held, the button offers to release it; when it says
 * a party is being created, the screen offers exactly the choices that projection listed and shows the
 * rule the flow answered with when one of them was refused. Nothing here decides on its own that a
 * choice is illegal — the refusal is the product's answer, and a screen that hid it would be hiding the
 * game's rules.
 */

interface ProjectionEnvelope {
  readonly contract: string;
  readonly value: unknown;
}

interface ProductUiContext {
  readonly projection?: {
    subscribe(listener: (projection: ProjectionEnvelope | null) => void): () => void;
  };
  readonly intents?: {
    claim(
      intent: string,
      value: { kind: 'product-payload'; contract: string; data: Record<string, unknown> },
    ): void;
  };
}

/** The projection contract this companion renders; any other contract is not ours to interpret. */
const UI_CONTRACT = 'crawler.ui.snapshot.v1';

/** The intent and payload contract the product declares for semantic actions. */
const UI_ACTION_INTENT = 'crawler.ui';
const UI_ACTION_CONTRACT = 'crawler.ui.action.v1';

/** The session actions this companion can ask for. */
const ACTION_PAUSE = 'session.pause';
const ACTION_RESUME = 'session.resume';
const ACTION_SAVE = 'session.save';
const ACTION_USE = 'party.use';

/**
 * The act control: one press orders the party to attack what it can reach. It is held rather than pressed —
 * the product repeats while it is down — so a player who keeps it down keeps attacking as each member's own
 * recovery elapses. The button sends the same action the B key does.
 */
const ACTION_ATTACK = 'party.attack';

/**
 * The pace controls: switching the one fight between real-time and turn-based pacing, and the two turn
 * actions a paced fight has beyond acting. Each is the name of an intent the product declared and the panel
 * action that asks for the same thing, so a key and a button are one control. Acting is deliberately not
 * among them: the act control above is the same control in both pacings, because the act is the same act
 * whoever holds the turn.
 */
const ACTION_TURN_BASED = 'combat.turn-based';
const ACTION_TURN_SKIP = 'combat.turn-skip';
const ACTION_TURN_WAIT = 'combat.turn-wait';

/**
 * The creation actions this companion reports. Each names a choice, the product's flow validates it, and
 * its refusal is what the screen shows. The names are the product's wire vocabulary: this companion sends
 * them and reads nothing back except the projection.
 */
const ACTION_SELECT_MEMBER = 'creation.select-member';
const ACTION_SELECT_PORTRAIT = 'creation.select-portrait';
const ACTION_SELECT_CLASS = 'creation.select-class';
const ACTION_SET_NAME = 'creation.set-name';
const ACTION_RAISE_ATTRIBUTE = 'creation.raise-attribute';
const ACTION_LOWER_ATTRIBUTE = 'creation.lower-attribute';
const ACTION_CHOOSE_SKILL = 'creation.choose-skill';
const ACTION_REMOVE_SKILL = 'creation.remove-skill';
const ACTION_ADVANCE = 'creation.advance';
const ACTION_ACCEPT = 'creation.accept';

/**
 * The service actions this companion reports while the party stands at a counter. Entering a service is not
 * one of them: the party enters by using the person it is facing, on the use control above, so the screen
 * only ever asks for what happens at a counter it was already shown.
 */
const ACTION_RAISE_SKILL = 'party.raise-skill';

/**
 * The casting actions this companion reports. A casting names the member, the spell, and the target the
 * product published for it — one action rather than a mode, because a spell and what it is aimed at are one
 * decision. The quick-slot action names the member and the spell its slot should hold, or none to clear it.
 */
const ACTION_CAST = 'party.cast';
const ACTION_QUICK_SPELL = 'party.quick-spell';
const ACTION_SERVICE_BUY = 'service.buy';
const ACTION_SERVICE_SELL = 'service.sell';
const ACTION_SERVICE_IDENTIFY = 'service.identify';
const ACTION_SERVICE_REPAIR = 'service.repair';
const ACTION_SERVICE_TEACH = 'service.teach';
const ACTION_SERVICE_TRAIN = 'service.train';
const ACTION_SERVICE_LEAVE = 'service.leave';

/**
 * The stop actions this companion reports. Every stop is its own control because every stop is a different
 * act: a rest heals and a wait does not, and one button cannot mean both. The names are the product's wire
 * vocabulary, exactly as the service actions are.
 */
const ACTION_CONVERSATION_TOPIC = 'conversation.topic';
const ACTION_CONVERSATION_PERSON = 'conversation.person';
const ACTION_CONVERSATION_LEAVE = 'conversation.leave';

const ACTION_REST = 'rest.rest';
const ACTION_CAMP = 'rest.camp';
const ACTION_WAIT_DAWN = 'rest.wait-dawn';
const ACTION_WAIT_HOUR = 'rest.wait-hour';
const ACTION_WAIT_FIVE_MINUTES = 'rest.wait-five-minutes';

interface CompositionView {
  readonly ruleset: string;
  readonly title: string;
  /** The game bundle the product started from, empty when no bundle was selected. */
  readonly bundle: string;
  readonly contentPacks: number;
}

/** The world the party is in. Empty when the session has no world loaded. */
interface WorldView {
  readonly place: string;
  readonly name: string;
  readonly kind: string;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly yaw: number;
  readonly visited: number;
  readonly places: number;
  /** Whether the place's own buildings are open at the hour this projection was built; a clock read. */
  readonly open: boolean;
  /** The hours the place keeps, empty when content clocks nothing in it. */
  readonly hours: string;
  /** When those hours next change, as a point on the game calendar, empty when nothing does. */
  readonly nextChange: string;
}

interface SessionView {
  readonly mode: string;
  readonly simulationSeconds: number;
  readonly admittedSteps: number;
  readonly updates: number;
}

/**
 * What the party's last admitted movement step did, as the panel shows it. `motion` is `none` when the
 * session has no movement facts at all — no world, or no step yet — and the remaining fields then
 * describe no step rather than a quiet one.
 */
interface MovementView {
  /** The state the last step left the party in: `none`, `grounded`, or `airborne`. */
  readonly motion: string;
  /** What refused the step: a reason word, or `none` when nothing did. */
  readonly blocked: string;
  /** How high an accepted step-up raised the party; zero when the engine accepted none. */
  readonly stepRise: number;
  readonly fallDistance: number;
  readonly fallDamage: number;
}

/**
 * Where the game clock stands, as the product published it. `present` is false when the session's
 * ruleset composed no clock, and then the remaining fields carry nothing to show.
 */
interface ClockView {
  readonly present: boolean;
  /** The date on the clock's calendar, as the calendar's own numbers. */
  readonly date: string;
  /** The time of day, whole minutes. */
  readonly time: string;
  /** Which half of the daylight window the clock stands in: `day` or `night`. */
  readonly daylight: string;
  /** Whole game days elapsed since the session began. */
  readonly elapsedDays: number;
}

/**
 * The party's own accounts and standing, as the product published them. `present` is false when the
 * session holds no party, which is what content that declares none gets.
 */
interface PartyView {
  readonly present: boolean;
  readonly members: number;
  /** How many items lie in the party's one shared pack, which is where everything the party takes goes. */
  readonly pack: number;
  readonly coins: number;
  readonly provisions: number;
  /** The unit the provisions are stated in, so the number is never shown without its measure. */
  readonly unit: string;
  readonly reputation: number;
  readonly fame: number;
  /** The conditions acting on the party, empty when none act. */
  readonly conditions: string;
  /** What the members have left to lose between them, and the measure the first number needs. */
  readonly hitPoints: number;
  readonly hitPointsMax: number;
  /** What the members have left to cast with between them. */
  readonly spellPoints: number;
  readonly spellPointsMax: number;
}

/**
 * One member's growth, as the product published it: the level, what earned it, and what the next costs. The
 * panel prints these numbers and derives none of them — the experience a level takes is the ruleset's own
 * curve and the fee is the counter's own quote, both read from the projection.
 */
interface ProgressionMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  readonly level: number;
  /** How much experience the member has earned in total. */
  readonly experience: number;
  /** How many skill points the member holds unspent. */
  readonly skillPoints: number;
  /** How much experience the member's next level takes. */
  readonly nextLevel: number;
  /** What the counter the party stands at would charge this member for a level; zero when none trains. */
  readonly fee: number;
  /** The highest level that counter trains to; zero when none trains. */
  readonly cap: number;
}

/**
 * What the party has earned and what a level costs, as the product published it. `available` is false when
 * the session holds no progression owner, which is what a ruleset that answered no progression policy gets.
 */
interface ProgressionView {
  readonly available: boolean;
  readonly members: readonly ProgressionMemberView[];
  /** What the last progression event was: `none`, `awarded`, `trained`, or `refused`. */
  readonly outcome: string;
  /** What the last award came from, or the counter that last trained somebody. */
  readonly source: string;
  /** How much experience the last award was worth, zero when the last event was not one. */
  readonly earned: number;
  readonly code: string;
  readonly message: string;
}

/**
 * How the session stands with its save slot, as the product published it. `state` is `none` until a save
 * is asked for, `saved` when one landed, and `failed` when one did not — and `message` then says what went
 * wrong, because a save that could not land must not look like one that did.
 */
interface SaveView {
  /** Whether the session has a save store at all. */
  readonly available: boolean;
  /** Whether this session was composed from the save in its slot rather than started fresh. */
  readonly resumed: boolean;
  /** The slot a save is written to. */
  readonly slot: string;
  /** `none`, `saved`, or `failed`. */
  readonly state: string;
  /** The game date and time the last save landed, empty when none has. */
  readonly at: string;
  /** Which failure this was, empty unless the state is `failed`. */
  readonly code: string;
  /** What happened, in the terms of the save. */
  readonly message: string;
}

/**
 * What the party is facing and what using it did, as the product published it. `available` is false when the
 * session holds no interaction at all, `reason` says why the reticle holds or refuses what it does, and
 * `outcome`, `code`, `message`, and `residue` describe the last use — a refusal keeps its own code and
 * sentence, because a use that silently did nothing must not look like one that did.
 */
interface InteractionView {
  /** Whether the session holds an interaction mechanism at all. */
  readonly available: boolean;
  /** The focused target's kind, empty when nothing is focused. */
  readonly target: string;
  /** What the focused target is called, empty when nothing is focused. */
  readonly label: string;
  /** The use that applies to it, empty when nothing is focused. */
  readonly verb: string;
  /** What the party has already done to it. */
  readonly state: string;
  /** How far it stands from the party, zero when nothing is focused. */
  readonly distance: number;
  /** Why the reticle holds or refuses what it does. */
  readonly reason: string;
  /** What the focused target requires, in the order the checks happen. */
  readonly requires: readonly string[];
  /**
   * How many bodies lie in the place the party stands in. A body is what the party killed, and a place whose
   * only usable thing is one would otherwise read exactly like an empty room.
   */
  readonly bodies: number;
  /** `none`, `applied`, or `refused`. */
  readonly outcome: string;
  /** The last refusal's code, empty when the last use applied or none has happened. */
  readonly code: string;
  /** What the last use reported. */
  readonly message: string;
  /** What the last use could not deliver. */
  readonly residue: string;
}

/** One line of a service's shelves, as the product published it. */
interface ServiceStockView {
  /** The lot's identity, which a buy command names. */
  readonly lot: string;
  readonly item: string;
  /** What a person reads for it. */
  readonly name: string;
  /** How many are left on the shelves. */
  readonly count: number;
  /** What one costs the party. */
  readonly price: number;
  /** Whether the counter is reselling something it bought from the party. */
  readonly sale: boolean;
}

/** One lesson a service teaches. */
interface ServiceLessonView {
  /** `skill` or `effect`. */
  readonly kind: string;
  /** The skill's or effect's id, which a teach command names. */
  readonly subject: string;
  readonly name: string;
  readonly amount: number;
  readonly price: number;
  /** The rung of a skill's ladder the lesson leaves a member at, where one is the first rung. */
  readonly tier: number;
}

/** One of the party's own items, as a counter that would buy it shows it. */
interface ServiceSaleView {
  /** The instance's durable identity, which a sell, identify, or repair command names. */
  readonly item: string;
  readonly definition: string;
  readonly name: string;
  /** What the counter would pay the party for it. */
  readonly price: number;
  readonly damage: number;
  readonly identified: boolean;
}

/** One member a lesson could be taught to. */
interface ServiceMemberView {
  readonly index: number;
  readonly name: string;
}

/**
 * The counter the party is standing at, as the product published it. `available` is false when the session
 * holds no service mechanism at all; `open` says whether a visit is; `state` says whether the counter is
 * serving, which a shop that closed while the party browsed changes under it. Every price and every list
 * here arrived in the projection: the screen decides nothing and sends back only what it was shown.
 */
interface ServiceView {
  readonly available: boolean;
  readonly open: boolean;
  readonly id: string;
  readonly kind: string;
  readonly name: string;
  readonly proprietor: string;
  /** `open`, `closed`, or empty when the party stands at no counter. */
  readonly state: string;
  /** The hours content states for it, empty when it serves always. */
  readonly hours: string;
  readonly operations: readonly string[];
  readonly memberships: readonly string[];
  readonly stock: readonly ServiceStockView[];
  readonly lessons: readonly ServiceLessonView[];
  readonly sales: readonly ServiceSaleView[];
  readonly members: readonly ServiceMemberView[];
  /** What the last command asked for, empty before any. */
  readonly action: string;
  /** `none`, `applied`, or `refused`. */
  readonly outcome: string;
  /** The last refusal's code, empty when the last command applied or none has happened. */
  readonly code: string;
  /** What the last command reported. */
  readonly message: string;
  readonly paid: number;
  readonly earned: number;
  /** What the party's one purse holds. */
  readonly coins: number;
}

/**
 * What the party's last stop did, what it cost, and what going without sleep is doing to it, as the product
 * published it. `available` is false when the session holds no rest mechanism at all; `outcome` is `none`
 * until the party has stopped; and a refusal keeps its own code and sentence, because a stop that silently
 * did nothing must not look like one that did. `kind` is what was asked for, `interrupted` says a night was
 * broken, and the fatigue facts are the clock's own deadline rather than a count kept here.
 */
/** One person present in a conversation, as the product publishes them. */
interface ConversationPersonView {
  readonly id: string;
  readonly name: string;
  readonly portrait: string;
  readonly speaking: boolean;
}

/** One topic a speaker has: on offer, or withheld with the reason the state gives. */
interface ConversationTopicView {
  readonly id: string;
  readonly label: string;
  readonly available: boolean;
  readonly reason: string;
}

/** One thing said while the conversation has been open. */
interface ConversationLineView {
  readonly speaker: string;
  readonly text: string;
  readonly residue: string;
}

/**
 * The conversation block as the product publishes it: who is here, what was said, which topics are on
 * offer, and which the state withholds with its reason. The companion renders the offers as choices and
 * the withheld topics as the reasons they are not, and decides nothing about either.
 */
interface ConversationView {
  readonly available: boolean;
  readonly open: boolean;
  readonly subject: string;
  readonly speaker: string;
  readonly greeting: string;
  readonly people: readonly ConversationPersonView[];
  readonly topics: readonly ConversationTopicView[];
  readonly withheld: readonly ConversationTopicView[];
  readonly said: readonly ConversationLineView[];
  readonly action: string;
  readonly outcome: string;
  readonly code: string;
  readonly message: string;
  readonly residue: string;
  readonly handoff: string;
  readonly topic: string;
}

/** One actor of a fight, as the fight published it. */
interface FighterView {
  readonly id: string;
  readonly name: string;
  /** Whether the actor may act now, as the fight's own recovery says. */
  readonly ready: boolean;
  /** How much game time it must still recover, zero when it is ready. */
  readonly recoverySeconds: number;
  /** How far it stands from the party, in the place's own units. */
  readonly distance: number;
  /** What the actor has left to lose, as the product read it from wherever that is owned. */
  readonly hitPoints: number;
  /** What it can take altogether, zero when nothing here states it. */
  readonly hitPointsMax: number;
  /** What is acting on the actor, in the product's own words, empty when nothing is. */
  readonly conditions: string;
  /** Whether the actor is out of the fight: laid out by what is on it, or taken down by harm. */
  readonly down: boolean;
  /**
   * What the actor is doing, for an actor the product drives: `closing`, `backing away`, `holding`,
   * `attacking`, or `down`; empty for the party's own members, whose doing is the player's.
   */
  readonly activity: string;
}

/**
 * One actor's place in the order a paced round acts in, as the product published it.
 *
 * The remaining recovery is the fight's own quantity rather than a bar this screen fills: a row that counted
 * down for itself would show a turn arriving before the product agreed, and the order is the fight's own
 * reading of the same recovery that paces real time.
 */
interface TurnOrderView {
  readonly id: string;
  readonly name: string;
  /** `party`, `opposition`, or `neutral`, as the fight reads the actor's side. */
  readonly side: string;
  /** How much game time it must still recover, zero when it is ready. */
  readonly remainingSeconds: number;
  /** Whether it may act now. */
  readonly ready: boolean;
  /** Whether the fight leaves it able to act at all. */
  readonly canAct: boolean;
  /** Whether it has deferred its turn to the end of the round. */
  readonly waiting: boolean;
  /** Whether this is the actor whose turn it is. */
  readonly current: boolean;
}

/**
 * The round a paced fight is in, as the product published it: which phase, whose turn, and what is left to
 * act. `phase` is `none` while the fight is being played in real time, which is a different fact from a
 * round nobody's turn is in.
 */
interface TurnView {
  /** `none`, `action`, or `movement`. */
  readonly phase: string;
  /** Which round is being fought, counting from one, or zero when none is. */
  readonly round: number;
  /** The identity of the actor whose turn it is, empty when no turn is out. */
  readonly actor: string;
  /** What that actor is called. */
  readonly actorName: string;
  /**
   * Whether the product is waiting for the player's committed turn. While it holds, the world does not step
   * and game time does not pass — so a screen that showed it moving on would be showing what is not
   * happening.
   */
  readonly playerTurn: boolean;
  /** How much game time passes before the current turn, zero when it is due now. */
  readonly dueSeconds: number;
  /** How long this round's action phase is. */
  readonly roundSeconds: number;
  /** How much of it has passed. */
  readonly elapsedSeconds: number;
  /** What is left of the party's movement phase. */
  readonly movementSeconds: number;
  /** What the party's last committed turn did: `act`, `skip`, `wait`, or empty before one. */
  readonly last: string;
  /** The fight's actors in the order they act, ascending remaining recovery. */
  readonly order: readonly TurnOrderView[];
}

/**
 * The fight the party is in, as the product published it: who is in it, who may act, what the last order
 * did, and which pacing it is being played in. Every number here is the product's — the panel runs no
 * countdown of its own, because a screen that ticked a recovery down for itself would show a character ready
 * before the product says so.
 */
interface CombatView {
  /** Whether the session holds a fight mechanism at all. */
  readonly available: boolean;
  /** Whether anything is fighting the party right now. */
  readonly engaged: boolean;
  /** How many actors are fighting the party. */
  readonly opposition: number;
  /** How many of the party's members may act now. */
  readonly ready: number;
  /** The party's members, in the order the fight reads them. */
  readonly members: readonly FighterView[];
  /** The actors fighting the party. */
  readonly enemies: readonly FighterView[];
  /** Who attacked last, empty before the party has attacked. */
  readonly actor: string;
  /** How the last attack was made: `melee`, `ranged`, or `spell`; empty before any. */
  readonly kind: string;
  /** What the last attack was aimed at, empty when it was aimed at nothing. */
  readonly target: string;
  /** `none`, `applied`, or `refused`. */
  readonly outcome: string;
  /** The last refusal's code, empty when the last order applied or none has been given. */
  readonly code: string;
  /** What the last order reported. */
  readonly message: string;
  /** What the last attack cost in game time, zero for a refusal. */
  readonly recoverySeconds: number;
  /** Whether the last attack was resolved at all, which a fight with no resolution never is. */
  readonly resolved: boolean;
  /** Whether the last resolved attack landed. */
  readonly hit: boolean;
  /** How likely the product said it was to land, in ten-thousandths. */
  readonly chance: number;
  /** What the last attack's damage dice rolled, before resistance. */
  readonly damageRolled: number;
  /** How much harm the last attack left. */
  readonly damage: number;
  /** What kind of harm it did, empty when nothing was resolved. */
  readonly damageKind: string;
  /** What the target resisted: `immune`, a weight, or empty when nothing was resolved. */
  readonly resistance: string;
  /** What the last hit left on its target besides harm, empty when it left nothing. */
  readonly condition: string;
  /** Whether the last hit is what took its target down. */
  readonly targetDown: boolean;
  /**
   * Whether the last order was the party's own. A fight is two-sided, so the last thing that happened may
   * be a blow the party took, and a panel that could not tell the two apart would show a wound with no
   * author.
   */
  readonly byParty: boolean;
  /** Which pacing the one fight is being played in: `realtime` or `turnbased`. */
  readonly pacing: string;
  /** The round it is in, or the no-round value while it is played in real time. */
  readonly turn: TurnView;
}

interface RestView {
  readonly available: boolean;
  /** What the last stop asked for: `rest`, `camp`, `wait-dawn`, `wait-hour`, or `wait-five-minutes`. */
  readonly kind: string;
  /** `none`, `applied`, or `refused`. */
  readonly outcome: string;
  /** The last refusal's code, empty when the last stop applied or none has happened. */
  readonly code: string;
  /** What the last stop reported. */
  readonly message: string;
  /** Where the clock stood when the stop was asked for, and where it stands now. */
  readonly from: string;
  readonly to: string;
  /** How much game time the stop covered. */
  readonly elapsedSeconds: number;
  /** What the larder was charged and what it covered, in the unit the party's food is measured in. */
  readonly charged: number;
  readonly covered: number;
  readonly unit: string;
  readonly interrupted: boolean;
  readonly recovered: boolean;
  /** How many members a completed sleep restored. */
  readonly restored: number;
  /** The conditions a completed sleep cleared, empty when it cleared none. */
  readonly cleared: string;
  /** The state the larder's own rule left on the party, empty when it left none. */
  readonly shortage: string;
  /** Whether the party currently carries the state going without sleep puts on it. */
  readonly tired: boolean;
  /** When the debt of sleep next falls due, empty while the party is asleep. */
  readonly fatigueDue: string;
  /** How many times the debt has fallen due since the session began. */
  readonly fatigueLanded: number;
}

/** One member of the party being created, as the flow published it. */
interface CreationMemberView {  readonly index: number;
  /** Where this member stands: `portrait`, `class`, `name`, `attributes`, `skills`, or `complete`. */
  readonly step: string;
  readonly name: string;
  /** The race the portrait decided, empty while no portrait has been chosen. */
  readonly race: string;
  /** The class chosen, empty while none has been. */
  readonly class: string;
  readonly portrait: string;
  /** How many attribute points this member has still to spend. */
  readonly pool: number;
}

/** One member of the party a session accepted, as it stands in the party being played. */
interface CreationPartyMemberView {
  readonly index: number;
  readonly name: string;
  readonly race: string;
  readonly class: string;
  readonly portrait: string;
}

/** One portrait creation offers. */
interface CreationPortraitView {
  readonly id: string;
  readonly name: string;
  readonly race: string;
  readonly selected: boolean;
}

/** One class creation offers. */
interface CreationClassView {
  readonly id: string;
  readonly name: string;
  readonly selected: boolean;
}

/** One skill of the member being created, and where it stands: `fixed`, `chosen`, or `available`. */
interface CreationSkillView {
  readonly id: string;
  readonly name: string;
  readonly state: string;
}

/** One attribute of the member being created, with the range its race allows. */
interface CreationAttributeView {
  readonly id: string;
  readonly name: string;
  readonly value: number;
  readonly minimum: number;
  readonly maximum: number;
  readonly canRaise: boolean;
  readonly canLower: boolean;
}

/**
 * The creation screen, as the product published it: where the flow stands, what the member being created
 * has chosen, what it may still choose, the rule the last illegal choice broke, and — once a party has
 * been accepted — the members of the party being played.
 */
interface CreationView {
  readonly active: boolean;
  readonly accepted: boolean;
  readonly hasDefault: boolean;
  readonly member: number;
  readonly members: number;
  readonly step: string;
  readonly pool: number;
  readonly refusalCode: string;
  readonly refusalMessage: string;
  readonly roster: readonly CreationMemberView[];
  readonly portraits: readonly CreationPortraitView[];
  readonly classes: readonly CreationClassView[];
  readonly skills: readonly CreationSkillView[];
  readonly attributes: readonly CreationAttributeView[];
  readonly party: readonly CreationPartyMemberView[];
}

interface SnapshotView {
  readonly composition: CompositionView;
  readonly session: SessionView;
  readonly world: WorldView;
  readonly movement: MovementView;
  readonly clock: ClockView;
  readonly party: PartyView;
  readonly creation: CreationView;
  readonly save: SaveView;
  readonly interaction: InteractionView;
  readonly service: ServiceView;
  readonly rest: RestView;
  readonly conversation: ConversationView;
  readonly combat: CombatView;
  readonly progression: ProgressionView;
  readonly skills: SkillsView;
  readonly magic: MagicView;
}

/** The clock of a session that has none, which the panel shows as not knowing rather than as a date. */
const CLOCK_UNKNOWN: ClockView = {
  present: false,
  date: '',
  time: '',
  daylight: '',
  elapsedDays: 0,
};

/** The party of a session that holds none. */
const PARTY_UNKNOWN: PartyView = {
  present: false,
  members: 0,
  pack: 0,
  coins: 0,
  provisions: 0,
  unit: '',
  reputation: 0,
  fame: 0,
  conditions: '',
  hitPoints: 0,
  hitPointsMax: 0,
  spellPoints: 0,
  spellPointsMax: 0,
};

/**
 * The skill-spend control this companion reports, on the product's own action contract: the screen names the
 * member it drew and the skill on that row, and the product judges the ceiling and the price.
 */
const ACTION_RAISE = 'party.raise-skill';

/** One skill a member holds, as the product published it. */
interface SkillRowView {
  readonly skill: string;
  /** `weapon`, `armour`, `magic`, `miscellaneous`, or `unused`. */
  readonly block: string;
  readonly level: number;
  /** What the rung the member stands at is called, in the product's own word. */
  readonly tier: string;
  readonly ceilingLevel: number;
  /** What the highest rung the member's class and rank allow is called. */
  readonly ceilingTier: string;
  readonly pointsSpent: number;
  /** The level one more point would leave the skill at, as the product worked it out. */
  readonly reached: number;
  /** What one more level would cost, zero when it would be refused. */
  readonly cost: number;
  /** Why one more level would be refused, empty when it would land. */
  readonly refusal: string;
}

/** One member's skills, as the product published them. */
interface SkillMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  readonly class: string;
  readonly rank: number;
  readonly skills: readonly SkillRowView[];
}

/**
 * What every member can hold and what the next point would buy, as the product published it. `available` is
 * false when the session's ruleset stated no skill policy, which is what a game that never said how far a
 * skill may grow gets.
 */
interface SkillsView {
  readonly available: boolean;
  readonly members: readonly SkillMemberView[];
  /** What the last raise was: `none`, `raised`, or `refused`. */
  readonly outcome: string;
  readonly member: string;
  readonly skill: string;
  readonly level: number;
  readonly cost: number;
  readonly code: string;
  readonly message: string;
}

/** Something a spell whose aim names no actor may be pointed at, as the product offered it. */
interface SpellAimView {
  /** The identity a casting echoes back to choose this. */
  readonly aim: string;
  /** What a person reads for it. */
  readonly name: string;
  /** What sort of thing it is, as the game's own word. */
  readonly kind: string;
}

/** One effect a spell has left running on the party, as the product published it. */
interface SpellRunningView {
  /** The effect identity the spell left, which the panel shows and never interprets. */
  readonly effect: string;
  /** The magnitude it acts at. */
  readonly magnitude: number;
  /** When the clock ends it, empty when nothing has said. */
  readonly endsAt: string;
}

/** One reading a cast left behind, as the product published it. */
interface SpellFactView {
  /** What the reading is about. */
  readonly name: string;
  /** What it reads as. */
  readonly value: string;
}

/** One spell a member knows, as the product published it. */
interface SpellRowView {
  readonly spell: string;
  readonly name: string;
  readonly school: string;
  /** The rung of the school's ladder the spell asks for, as the game's own word. */
  readonly tier: string;
  readonly tierRung: number;
  /** What one casting costs this caster, as the product's own answer for that member. */
  readonly cost: number;
  /** What the spell is aimed at: `none`, `caster`, `ally`, `foe`, or `party`. */
  readonly targeting: string;
  /** The effect identity the spell carries, which the panel shows and never interprets. */
  readonly effect: string;
  /** What it may be pointed at when its aim names no actor, empty when its aim names nothing. */
  readonly aims: readonly SpellAimView[];
}

/** One member's spellbook and what casting from it costs. */
interface MagicMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  readonly class: string;
  readonly spellPoints: number;
  readonly spellPointsMax: number;
  readonly quickSpell: string;
  readonly quickSpellName: string;
  readonly spells: readonly SpellRowView[];
}

/** One actor a casting may be aimed at, with the side it is on. */
interface SpellTargetView {
  readonly target: string;
  readonly name: string;
  /** Which side the actor is on: `party` or `opposition`. */
  readonly side: string;
}

/** What the party can cast, what it may aim at, and what the last casting did. */
interface MagicView {
  readonly available: boolean;
  readonly members: readonly MagicMemberView[];
  readonly targets: readonly SpellTargetView[];
  readonly outcome: string;
  readonly member: number;
  readonly caster: string;
  readonly spell: string;
  readonly cost: number;
  readonly target: string;
  readonly effect: string;
  readonly code: string;
  readonly message: string;
  /** What the last casting changed, as readings of the state it changed. */
  readonly facts: readonly SpellFactView[];
  /** The effects spells have left running on the party, in the order they were applied. */
  readonly running: readonly SpellRunningView[];
  /** What the party sees by: `daylight`, `light`, `dark`, or empty when nothing states it. */
  readonly sight: string;
}

/** The skills of a session whose ruleset stated no skill policy: nothing may be raised. */
const SKILLS_NONE: SkillsView = {
  available: false,
  members: [],
  outcome: 'none',
  member: '',
  skill: '',
  level: 0,
  cost: 0,
  code: '',
  message: '',
};

/** The progression of a session that holds no owner: nobody grows and no level has a price. */
const PROGRESSION_NONE: ProgressionView = {
  available: false,
  members: [],
  outcome: 'none',
  source: '',
  earned: 0,
  code: '',
  message: '',
};

/** The save state of a projection that carries none: a session this companion cannot read as saveable. */
const SAVE_NONE: SaveView = {
  available: false,
  resumed: false,
  slot: '',
  state: 'none',
  at: '',
  code: '',
  message: '',
};

/** The interaction of a session that holds no mechanism, or one this companion cannot read as interactive. */
const INTERACTION_NONE: InteractionView = {  available: false,
  target: '',
  label: '',
  verb: '',
  state: '',
  distance: 0,
  reason: '',
  requires: [],
  bodies: 0,
  outcome: 'none',
  code: '',
  message: '',
  residue: '',
};

/** The creation of a session that is neither creating a party nor playing one it accepted. */
const CREATION_NONE: CreationView = {
  active: false,
  accepted: false,
  hasDefault: false,
  member: 0,
  members: 0,
  step: '',
  pool: 0,
  refusalCode: '',
  refusalMessage: '',
  roster: [],
  portraits: [],
  classes: [],
  skills: [],
  attributes: [],
  party: [],
};

/** The service of a session that holds no mechanism, or one this companion cannot read as a counter. */
const SERVICE_NONE: ServiceView = {
  available: false,
  open: false,
  id: '',
  kind: '',
  name: '',
  proprietor: '',
  state: '',
  hours: '',
  operations: [],
  memberships: [],
  stock: [],
  lessons: [],
  sales: [],
  members: [],
  action: '',
  outcome: 'none',
  code: '',
  message: '',
  paid: 0,
  earned: 0,
  coins: 0,
};

/** The fight of a session that holds no mechanism, or one this companion cannot read as a fight. */
/** The round of a fight that is being played in real time, or of one this companion cannot read. */
const TURN_NONE: TurnView = {
  phase: '',
  round: 0,
  actor: '',
  actorName: '',
  playerTurn: false,
  dueSeconds: 0,
  roundSeconds: 0,
  elapsedSeconds: 0,
  movementSeconds: 0,
  last: '',
  order: [],
};

const COMBAT_NONE: CombatView = {
  available: false,
  engaged: false,
  opposition: 0,
  ready: 0,
  members: [],
  enemies: [],
  actor: '',
  kind: '',
  target: '',
  outcome: 'none',
  code: '',
  message: '',
  recoverySeconds: 0,
  resolved: false,
  hit: false,
  chance: 0,
  damageRolled: 0,
  damage: 0,
  damageKind: '',
  resistance: '',
  condition: '',
  targetDown: false,
  byParty: false,
  pacing: 'realtime',
  turn: TURN_NONE,
};

/** The rest of a session that holds no mechanism, or one this companion cannot read as a stop. */
/** A session with no conversation mechanism: there is nobody to speak with and nothing to say. */
const CONVERSATION_NONE: ConversationView = {
  available: false,
  open: false,
  subject: '',
  speaker: '',
  greeting: '',
  people: [],
  topics: [],
  withheld: [],
  said: [],
  action: '',
  outcome: 'none',
  code: '',
  message: '',
  residue: '',
  handoff: '',
  topic: '',
};

const REST_NONE: RestView = {
  available: false,
  kind: '',
  outcome: 'none',
  code: '',
  message: '',
  from: '',
  to: '',
  elapsedSeconds: 0,
  charged: 0,
  covered: 0,
  unit: '',
  interrupted: false,
  recovered: false,
  restored: 0,
  cleared: '',
  shortage: '',
  tired: false,
  fatigueDue: '',
  fatigueLanded: 0,
};

const STYLES = `
.crawler-session {
  position: fixed;
  top: 0.75rem;
  left: 0.75rem;
  min-width: 15rem;
  max-width: 24rem;
  /* The creation screen is taller than a short viewport, and a panel that runs off the bottom would put
     the flow's own controls where nobody can reach them. */
  max-height: calc(100vh - 1.5rem);
  overflow-y: auto;
  padding: 0.6rem 0.75rem;
  border: 1px solid rgba(210, 196, 158, 0.35);
  border-radius: 0.35rem;
  background: rgba(18, 16, 14, 0.82);
  color: #e8e0cc;
  font: 13px/1.45 system-ui, sans-serif;
}
.crawler-session h1 { margin: 0 0 0.15rem; font-size: 1rem; letter-spacing: 0.02em; }
.crawler-session .crawler-ruleset { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.78rem; }
.crawler-session .crawler-bundle { margin: 0 0 0.6rem; color: #8d8a7a; font-size: 0.72rem; letter-spacing: 0.02em; }
.crawler-session .crawler-place { margin: 0 0 0.5rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-session dl { display: grid; grid-template-columns: auto 1fr; gap: 0.1rem 0.6rem; margin: 0 0 0.5rem; }
.crawler-session dt { color: #b9ad8c; }
.crawler-session dd { margin: 0; text-align: right; font-variant-numeric: tabular-nums; }
.crawler-session[data-mode='paused'] { border-color: rgba(226, 176, 96, 0.6); }
.crawler-session[data-mode='creating'] { border-color: rgba(150, 200, 226, 0.6); }
.crawler-session button {
  width: 100%;
  padding: 0.3rem 0.5rem;
  border: 1px solid rgba(210, 196, 158, 0.5);
  border-radius: 0.25rem;
  background: rgba(60, 54, 44, 0.9);
  color: inherit;
  font: inherit;
  cursor: pointer;
}
.crawler-session button:disabled { opacity: 0.55; cursor: default; }
.crawler-session .crawler-hint { margin: 0.4rem 0 0; color: #b9ad8c; font-size: 0.72rem; }
.crawler-creation { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-creation[hidden] { display: none; }
.crawler-creation .crawler-step-head { margin: 0 0 0.35rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-creation .crawler-row { margin: 0 0 0.25rem; }
.crawler-creation .crawler-row-label { display: block; color: #b9ad8c; font-size: 0.72rem; }
.crawler-creation .crawler-options { display: flex; flex-wrap: wrap; gap: 0.2rem; }
.crawler-creation .crawler-options button { width: auto; padding: 0.1rem 0.35rem; font-size: 0.72rem; }
.crawler-creation .crawler-options button[data-selected='true'] { border-color: rgba(226, 176, 96, 0.9); background: rgba(96, 78, 50, 0.9); }
.crawler-creation .crawler-options button[data-available='false'] { opacity: 0.7; }
.crawler-creation .crawler-fixed { color: #8d8a7a; font-size: 0.75rem; }
.crawler-creation .crawler-attribute { display: flex; align-items: center; gap: 0.3rem; font-size: 0.72rem; line-height: 1.3; }
.crawler-creation .crawler-attribute span { flex: 1; }
.crawler-creation .crawler-attribute button { width: 1.3rem; padding: 0 0; font-size: 0.72rem; text-align: center; }
.crawler-creation .crawler-name { display: flex; gap: 0.2rem; margin-top: 0.2rem; }
/* A row the screen hides must actually be hidden: these two rows carry a display rule of their own, which
   without this would keep the accepting player's name box and the creation controls on screen after the
   party has been accepted, and would keep them in the tab order too. */
.crawler-creation .crawler-name[hidden] { display: none; }
.crawler-creation .crawler-actions[hidden] { display: none; }
.crawler-creation .crawler-name input {
  flex: 1;
  min-width: 0;
  padding: 0.2rem 0.35rem;
  border: 1px solid rgba(210, 196, 158, 0.5);
  border-radius: 0.25rem;
  background: rgba(18, 16, 14, 0.9);
  color: inherit;
  font: inherit;
}
.crawler-creation .crawler-actions { display: flex; gap: 0.2rem; margin-top: 0.3rem; }
.crawler-creation .crawler-actions button { padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-refusal { margin: 0.4rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(226, 120, 96, 0.8); color: #e8c8b0; font-size: 0.75rem; }
.crawler-refusal[hidden] { display: none; }
.crawler-accepted { margin: 0.35rem 0 0; padding: 0; list-style: none; color: #d8cba6; font-size: 0.75rem; }
.crawler-session .crawler-save { margin: 0.3rem 0 0; }
.crawler-conversation { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-conversation[hidden] { display: none; }
.crawler-conversation .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-conversation-greeting { margin: 0 0 0.35rem; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(210, 196, 158, 0.5); color: #e6dcc0; font-size: 0.78rem; }
.crawler-conversation .crawler-row { display: flex; flex-wrap: wrap; gap: 0.2rem; margin: 0 0 0.3rem; }
.crawler-conversation .crawler-row button { width: auto; padding: 0.1rem 0.35rem; font-size: 0.72rem; }
.crawler-conversation .crawler-options { display: flex; flex-wrap: wrap; gap: 0.2rem; }
.crawler-conversation .crawler-options button { width: auto; padding: 0.15rem 0.4rem; font-size: 0.74rem; }
.crawler-withheld { margin: 0.3rem 0 0; padding: 0; list-style: none; color: #a89a78; font-size: 0.7rem; }
.crawler-said { margin: 0.35rem 0 0; padding: 0; list-style: none; color: #dcc9a0; font-size: 0.74rem; }
.crawler-conversation .crawler-actions { display: flex; gap: 0.2rem; margin-top: 0.3rem; }
.crawler-conversation .crawler-actions button { padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-conversation-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-conversation-result[hidden] { display: none; }
.crawler-conversation-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-conversation-residue { margin: 0.15rem 0 0; color: #b8a888; font-size: 0.7rem; }
.crawler-conversation-residue[hidden] { display: none; }
.crawler-service { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-service[hidden] { display: none; }
.crawler-service .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-service-state { margin: 0 0 0.2rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-service-access { margin: 0 0 0.2rem; color: #cfe0e8; font-size: 0.72rem; }
.crawler-service-member { display: flex; align-items: center; gap: 0.3rem; margin: 0 0 0.25rem; font-size: 0.72rem; }
.crawler-service-member[hidden] { display: none; }
.crawler-service .crawler-row { margin: 0 0 0.25rem; }
.crawler-service .crawler-row-label { display: block; color: #b9ad8c; font-size: 0.72rem; }
.crawler-service .crawler-options { display: flex; flex-wrap: wrap; gap: 0.2rem; }
.crawler-service .crawler-options button { width: auto; padding: 0.1rem 0.35rem; font-size: 0.72rem; }
.crawler-service .crawler-actions { display: flex; gap: 0.2rem; margin-top: 0.3rem; }
.crawler-service .crawler-actions button { padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-service-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-service-result[hidden] { display: none; }
.crawler-service-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-session .crawler-use { margin: 0.3rem 0.4rem 0 0; }
.crawler-use-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-use-result[hidden] { display: none; }
.crawler-use-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-use-residue { margin: 0.15rem 0 0; color: #b8a888; font-size: 0.7rem; }
.crawler-use-residue[hidden] { display: none; }
.crawler-save-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-save-result[hidden] { display: none; }
.crawler-save-result[data-state='failed'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-combat { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-combat[hidden] { display: none; }
.crawler-combat .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-combat-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-combat .crawler-actions button { width: auto; padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-combat-members, .crawler-combat-enemies { margin: 0.2rem 0 0; padding: 0; list-style: none; color: #cfc3a2; font-size: 0.72rem; }
.crawler-fighter[data-ready='no'] { color: #a8967a; }
.crawler-combat-turn { margin: 0.2rem 0 0.25rem; color: #cfc3a2; font-size: 0.75rem; }
.crawler-turn-order { margin: 0.2rem 0 0; padding: 0; list-style: none; color: #b9ad8c; font-size: 0.72rem; }
.crawler-turn-order .crawler-turn-actor[data-current='yes'] { color: #efe6c8; }
.crawler-turn-order .crawler-turn-actor[data-waiting='yes'] { font-style: italic; }
.crawler-combat-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-combat-result[hidden] { display: none; }
.crawler-combat-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-combat-result[data-by-party='no'] { border-color: rgba(226, 170, 96, 0.85); color: #ecd6ac; }
.crawler-fighter[data-activity='closing'] { color: #e2cba0; }
.crawler-fighter[data-activity='backing away'] { color: #b9c8a4; }
.crawler-fighter[data-activity='down'] { color: #8f8878; text-decoration: line-through; }
.crawler-progression { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-progression[hidden] { display: none; }
.crawler-progression .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-progression-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-progression-member { display: flex; align-items: center; gap: 0.4rem; margin: 0 0 0.2rem; font-size: 0.72rem; color: #cfc3a2; }
.crawler-progression-member .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-progression .crawler-train { width: auto; padding: 0.15rem 0.35rem; font-size: 0.7rem; }
.crawler-progression-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-progression-result[hidden] { display: none; }
.crawler-progression-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-skills { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-skills[hidden] { display: none; }
.crawler-skills .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-skills-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-skills-member { margin: 0 0 0.3rem; }
.crawler-skills-member .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-skill { display: flex; align-items: center; gap: 0.4rem; margin: 0 0 0.15rem; font-size: 0.72rem; color: #cfc3a2; }
.crawler-skill-refusal { color: #e8c8b0; }
.crawler-skills .crawler-raise { width: auto; padding: 0.15rem 0.35rem; font-size: 0.7rem; }
.crawler-skills-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-skills-result[hidden] { display: none; }
.crawler-skills-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-magic { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-magic[hidden] { display: none; }
.crawler-magic .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-magic-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-magic-member { margin: 0 0 0.3rem; }
.crawler-magic-member .crawler-row-label { display: block; color: #cfc3a2; font-size: 0.72rem; }
.crawler-spell { display: flex; flex-wrap: wrap; gap: 0.25rem; align-items: center; font-size: 0.7rem; color: #c6bb9c; }
.crawler-magic .crawler-cast, .crawler-magic .crawler-quick { width: auto; padding: 0.15rem 0.35rem; font-size: 0.7rem; }
.crawler-magic .crawler-target { font-size: 0.7rem; }
.crawler-magic-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-magic-result[hidden] { display: none; }
.crawler-magic-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
.crawler-magic-facts { margin: 0.2rem 0 0; padding-left: 1.1rem; color: #cfe0e8; font-size: 0.72rem; }
.crawler-magic-facts[hidden] { display: none; }
.crawler-magic-running { margin: 0.2rem 0 0; color: #b9c9a8; font-size: 0.72rem; }
.crawler-magic-running[hidden] { display: none; }
.crawler-rest { margin: 0 0 0.5rem; border-top: 1px solid rgba(210, 196, 158, 0.25); padding-top: 0.5rem; }
.crawler-rest[hidden] { display: none; }
.crawler-rest .crawler-step-head { margin: 0 0 0.2rem; color: #d8cba6; font-size: 0.82rem; }
.crawler-rest-state { margin: 0 0 0.25rem; color: #b9ad8c; font-size: 0.72rem; }
.crawler-rest .crawler-actions { display: flex; flex-wrap: wrap; gap: 0.2rem; }
.crawler-rest .crawler-actions button { width: auto; padding: 0.2rem 0.4rem; font-size: 0.75rem; }
.crawler-rest-result { margin: 0.3rem 0 0; padding: 0.25rem 0.4rem; border-left: 2px solid rgba(150, 200, 226, 0.8); color: #cfe0e8; font-size: 0.75rem; }
.crawler-rest-result[hidden] { display: none; }
.crawler-rest-result[data-outcome='refused'] { border-color: rgba(226, 120, 96, 0.8); color: #e8c8b0; }
`;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

/**
 * Reads a list the projection published, skipping entries that are not the shape this companion reads.
 * A list that is missing, or carries an entry that is not an object, is not a reason to reject the whole
 * projection: the screen shows the choices it can read and never invents the ones it cannot.
 */
/** The string entries of an array, ignoring anything else: a word list is not a list of records. */
function words(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string') : [];
}

function readList<T>(value: unknown, read: (entry: Record<string, unknown>) => T | null): T[] {
  if (!Array.isArray(value)) return [];
  const items: T[] = [];
  for (const entry of value) {
    if (!isRecord(entry)) continue;
    const item = read(entry);
    if (item !== null) items.push(item);
  }

  return items;
}

/**
 * Reads the clock block, or the not-known clock. A block that is missing, or that is present but does
 * not carry the facts this panel renders, is not a reason to reject the whole projection: the session it
 * describes has a clock this companion cannot read, and showing that is honest where refusing to render
 * everything else would hide the rest of the session behind it.
 */
function readClock(value: unknown): ClockView {
  if (!isRecord(value) || value.present !== true) return CLOCK_UNKNOWN;
  const { date, time, daylight, elapsedDays } = value;
  if (
    typeof date !== 'string' ||
    typeof time !== 'string' ||
    typeof daylight !== 'string' ||
    typeof elapsedDays !== 'number'
  ) {
    return CLOCK_UNKNOWN;
  }

  return { present: true, date, time, daylight, elapsedDays };
}

/** Reads the party block, or the not-known party, on the same terms as the clock block. */
function readParty(value: unknown): PartyView {
  if (!isRecord(value) || value.present !== true) return PARTY_UNKNOWN;
  const { members, coins, provisions, unit, reputation, fame, conditions } = value;
  // The pools are read with a fallback: a projection from a product that publishes no vitality is a party
  // this panel can still show, and reading a missing number as zero would claim an exhausted party.
  const hitPoints = typeof value.hitPoints === 'number' ? value.hitPoints : 0;
  const hitPointsMax = typeof value.hitPointsMax === 'number' ? value.hitPointsMax : 0;
  const spellPoints = typeof value.spellPoints === 'number' ? value.spellPoints : 0;
  const spellPointsMax = typeof value.spellPointsMax === 'number' ? value.spellPointsMax : 0;
  const pack = typeof value.pack === 'number' ? value.pack : 0;
  if (
    typeof members !== 'number' ||
    typeof pack !== 'number' ||
    typeof coins !== 'number' ||
    typeof provisions !== 'number' ||
    typeof unit !== 'string' ||
    typeof reputation !== 'number' ||
    typeof fame !== 'number' ||
    typeof conditions !== 'string'
  ) {
    return PARTY_UNKNOWN;
  }

  return {
    present: true,
    members,
    pack,
    coins,
    provisions,
    unit,
    reputation,
    fame,
    conditions,
    hitPoints,
    hitPointsMax,
    spellPoints,
    spellPointsMax,
  };
}

/**
 * Reads the save block, or the not-readable save. A block that is missing, or that is present but does not
 * carry the facts this panel renders, is not a reason to reject the whole projection: the session it
 * describes has a save this companion cannot read, and showing that is honest where refusing to render
 * everything else would hide the rest of the session behind it.
 */
function readSave(value: unknown): SaveView {
  if (!isRecord(value)) return SAVE_NONE;
  const { slot, state, at, code, message } = value;
  if (
    typeof slot !== 'string' ||
    typeof state !== 'string' ||
    typeof at !== 'string' ||
    typeof code !== 'string' ||
    typeof message !== 'string'
  ) {
    return SAVE_NONE;
  }

  return {
    available: value.available === true,
    resumed: value.resumed === true,
    slot,
    state,
    at,
    code,
    message,
  };
}

/**
 * Reads the interaction block, or the no-mechanism value. A block that is missing, or that is present but
 * does not carry the facts this panel renders, is not a reason to reject the whole projection: the session it
 * describes faces a world this companion cannot read, and saying so is honest where refusing to render
 * everything else would hide the rest of the session behind it.
 */
function readInteraction(value: unknown): InteractionView {
  if (!isRecord(value)) return INTERACTION_NONE;
  const { target, label, verb, state, reason, outcome, code, message, residue } = value;
  const distance = value.distance ?? 0;
  const bodies = value.bodies ?? 0;
  const requires = Array.isArray(value.requires)
    ? value.requires.filter((entry): entry is string => typeof entry === 'string')
    : [];
  if (
    typeof target !== 'string' ||
    typeof label !== 'string' ||
    typeof verb !== 'string' ||
    typeof state !== 'string' ||
    typeof distance !== 'number' ||
    typeof reason !== 'string' ||
    typeof bodies !== 'number' ||
    typeof outcome !== 'string' ||
    typeof code !== 'string' ||
    typeof message !== 'string' ||
    typeof residue !== 'string'
  ) {
    return INTERACTION_NONE;
  }

  return {
    available: value.available === true,
    target,
    label,
    verb,
    state,
    distance,
    reason,
    requires,
    bodies,
    outcome,
    code,
    message,
    residue,
  };
}

/**
 * Reads the progression block, or the no-owner progression. A block this companion cannot read is read as
 * the no-owner value rather than half-read: a panel that showed a member's level from a projection it did
 * not understand would be showing a number the product never published.
 */
function readProgression(value: unknown): ProgressionView {
  if (!isRecord(value) || value.available !== true) return PROGRESSION_NONE;
  const { outcome, source, code, message } = value;
  const earned = value.earned ?? 0;
  if (
    typeof outcome !== 'string' ||
    typeof source !== 'string' ||
    typeof earned !== 'number' ||
    typeof code !== 'string' ||
    typeof message !== 'string'
  ) {
    return PROGRESSION_NONE;
  }

  return {
    available: true,
    members: readList(value.members, (entry) =>
      typeof entry.index === 'number' &&
      typeof entry.member === 'string' &&
      typeof entry.name === 'string' &&
      typeof entry.level === 'number' &&
      typeof entry.experience === 'number' &&
      typeof entry.skillPoints === 'number' &&
      typeof entry.nextLevel === 'number' &&
      typeof entry.fee === 'number' &&
      typeof entry.cap === 'number'
        ? {
            index: entry.index,
            member: entry.member,
            name: entry.name,
            level: entry.level,
            experience: entry.experience,
            skillPoints: entry.skillPoints,
            nextLevel: entry.nextLevel,
            fee: entry.fee,
            cap: entry.cap,
          }
        : null),
    outcome,
    source,
    earned,
    code,
    message,
  };
}

/**
 * Reads the skills block, or the no-policy skills. A block this companion cannot read is read as no policy
 * rather than as a party whose skills cannot grow: the panel then shows nothing to raise, which is what a
 * ruleset that answered no skill policy actually published.
 */
function readSkills(value: unknown): SkillsView {
  if (!isRecord(value) || value.available !== true) return SKILLS_NONE;
  const { outcome, member, skill, level, cost, code, message } = value;
  if (
    typeof outcome !== 'string' ||
    typeof member !== 'string' ||
    typeof skill !== 'string' ||
    typeof level !== 'number' ||
    typeof cost !== 'number' ||
    typeof code !== 'string' ||
    typeof message !== 'string'
  ) {
    return SKILLS_NONE;
  }

  return {
    available: true,
    members: readList(value.members, (entry) =>
      typeof entry.index === 'number' &&
      typeof entry.member === 'string' &&
      typeof entry.name === 'string' &&
      typeof entry.class === 'string' &&
      typeof entry.rank === 'number'
        ? {
            index: entry.index,
            member: entry.member,
            name: entry.name,
            class: entry.class,
            rank: entry.rank,
            skills: readList(entry.skills, (row) =>
              typeof row.skill === 'string' &&
              typeof row.block === 'string' &&
              typeof row.level === 'number' &&
              typeof row.tier === 'string' &&
              typeof row.ceilingLevel === 'number' &&
              typeof row.ceilingTier === 'string' &&
              typeof row.pointsSpent === 'number' &&
              typeof row.reached === 'number' &&
              typeof row.cost === 'number' &&
              typeof row.refusal === 'string'
                ? {
                    skill: row.skill,
                    block: row.block,
                    level: row.level,
                    tier: row.tier,
                    ceilingLevel: row.ceilingLevel,
                    ceilingTier: row.ceilingTier,
                    pointsSpent: row.pointsSpent,
                    reached: row.reached,
                    cost: row.cost,
                    refusal: row.refusal,
                  }
                : null),
          }
        : null),
    outcome,
    member,
    skill,
    level,
    cost,
    code,
    message,
  };
}

/**
 * Reads the service block, or the empty service. The block is published in every mode, so a session that
 * holds no mechanism and one that stands at no counter both read as empty here — and the screen then shows
 * no counter at all, which is a different reading from a counter that is shut.
 */
/**
 * Reads the magic block, or the no-magic block. A block this companion cannot read is read as no magic
 * rather than as a party whose spellbook is empty: the panel then offers no casting, which is what a session
 * whose ruleset stated no magic policy gets.
 */
function readMagic(value: unknown): MagicView {
  if (!isRecord(value)) return NO_MAGIC;
  const number = (entry: unknown): number => (typeof entry === 'number' ? entry : 0);
  return {
    available: value.available === true,
    members: readList(value.members, (entry) => ({
      index: number(entry.index),
      member: typeof entry.member === 'string' ? entry.member : '',
      name: typeof entry.name === 'string' ? entry.name : '',
      class: typeof entry.class === 'string' ? entry.class : '',
      spellPoints: number(entry.spellPoints),
      spellPointsMax: number(entry.spellPointsMax),
      quickSpell: typeof entry.quickSpell === 'string' ? entry.quickSpell : '',
      quickSpellName: typeof entry.quickSpellName === 'string' ? entry.quickSpellName : '',
      spells: readList(entry.spells, (spell) => ({
        spell: typeof spell.spell === 'string' ? spell.spell : '',
        name: typeof spell.name === 'string' ? spell.name : '',
        school: typeof spell.school === 'string' ? spell.school : '',
        tier: typeof spell.tier === 'string' ? spell.tier : '',
        tierRung: number(spell.tierRung),
        cost: number(spell.cost),
        targeting: typeof spell.targeting === 'string' ? spell.targeting : 'none',
        effect: typeof spell.effect === 'string' ? spell.effect : '',
        aims: readList(spell.aims, (aim) => ({
          aim: typeof aim.aim === 'string' ? aim.aim : '',
          name: typeof aim.name === 'string' ? aim.name : '',
          kind: typeof aim.kind === 'string' ? aim.kind : '',
        })),
      })),
    })),
    targets: readList(value.targets, (entry) => ({
      target: typeof entry.target === 'string' ? entry.target : '',
      name: typeof entry.name === 'string' ? entry.name : '',
      side: typeof entry.side === 'string' ? entry.side : '',
    })),
    outcome: typeof value.outcome === 'string' ? value.outcome : 'none',
    member: number(value.member),
    caster: typeof value.caster === 'string' ? value.caster : '',
    spell: typeof value.spell === 'string' ? value.spell : '',
    cost: number(value.cost),
    target: typeof value.target === 'string' ? value.target : '',
    effect: typeof value.effect === 'string' ? value.effect : '',
    code: typeof value.code === 'string' ? value.code : '',
    message: typeof value.message === 'string' ? value.message : '',
    facts: readList(value.facts, (fact) => ({
      name: typeof fact.name === 'string' ? fact.name : '',
      value: typeof fact.value === 'string' ? fact.value : '',
    })),
    running: readList(value.running, (effect) => ({
      effect: typeof effect.effect === 'string' ? effect.effect : '',
      magnitude: number(effect.magnitude),
      endsAt: typeof effect.endsAt === 'string' ? effect.endsAt : '',
    })),
    sight: typeof value.sight === 'string' ? value.sight : '',
  };
}

/** The magic of a session whose ruleset stated no spell policy: nothing can be cast. */
const NO_MAGIC: MagicView = {
  available: false,
  members: [],
  targets: [],
  outcome: 'none',
  member: 0,
  caster: '',
  spell: '',
  cost: 0,
  target: '',
  effect: '',
  code: '',
  message: '',
  facts: [],
  running: [],
  sight: '',
};

function readService(value: unknown): ServiceView {
  if (!isRecord(value)) return SERVICE_NONE;
  const { id, kind, name, proprietor, state, hours, action, outcome, code, message } = value;
  const coins = value.coins ?? 0;
  const paid = value.paid ?? 0;
  const earned = value.earned ?? 0;
  if (
    typeof id !== 'string' ||
    typeof kind !== 'string' ||
    typeof name !== 'string' ||
    typeof proprietor !== 'string' ||
    typeof state !== 'string' ||
    typeof hours !== 'string' ||
    typeof action !== 'string' ||
    typeof outcome !== 'string' ||
    typeof code !== 'string' ||
    typeof message !== 'string' ||
    typeof coins !== 'number' ||
    typeof paid !== 'number' ||
    typeof earned !== 'number'
  ) {
    return SERVICE_NONE;
  }

  return {
    available: value.available === true,
    open: value.open === true,
    id,
    kind,
    name,
    proprietor,
    state,
    hours,
    operations: words(value.operations),
    memberships: words(value.memberships),
    stock: readList(value.stock, (entry) =>
      typeof entry.lot === 'string' &&
      typeof entry.item === 'string' &&
      typeof entry.name === 'string' &&
      typeof entry.count === 'number' &&
      typeof entry.price === 'number'
        ? {
            lot: entry.lot,
            item: entry.item,
            name: entry.name,
            count: entry.count,
            price: entry.price,
            sale: entry.sale === true,
          }
        : null),
    lessons: readList(value.lessons, (entry) =>
      typeof entry.kind === 'string' &&
      typeof entry.subject === 'string' &&
      typeof entry.name === 'string' &&
      typeof entry.amount === 'number' &&
      typeof entry.price === 'number'
        ? {
            kind: entry.kind,
            subject: entry.subject,
            name: entry.name,
            amount: entry.amount,
            price: entry.price,
            tier: typeof entry.tier === 'number' ? entry.tier : 1,
          }
        : null),
    sales: readList(value.sales, (entry) =>
      typeof entry.item === 'string' &&
      typeof entry.definition === 'string' &&
      typeof entry.name === 'string' &&
      typeof entry.price === 'number' &&
      typeof entry.damage === 'number'
        ? {
            item: entry.item,
            definition: entry.definition,
            name: entry.name,
            price: entry.price,
            damage: entry.damage,
            identified: entry.identified === true,
          }
        : null),
    members: readList(value.members, (entry) =>
      typeof entry.index === 'number' && typeof entry.name === 'string'
        ? { index: entry.index, name: entry.name }
        : null),
    action,
    outcome,
    code,
    message,
    paid,
    earned,
    coins,
  };
}

/**
 * Reads the creation block, or the empty creation. The block is published in every mode, so a session
 * that is doing neither — a resumed one — carries the empty shape and the screen shows no creation
 * section at all, which is a different reading from a party that is still being made.
 */
/**
 * Reads the rest block, or the no-mechanism value. A block that is missing, or that is present but does not
 * carry the facts this panel renders, is not a reason to reject the whole projection: the session it
 * describes stops through a mechanism this companion cannot read, and saying so is honest where refusing to
 * render everything else would hide the rest of the session behind it.
 */
/**
 * Reads the conversation block, or the empty conversation. The block is published in every mode, so a
 * session that holds no mechanism and one that is speaking with nobody both read as empty here — and the
 * screen then shows no conversation at all, which is a different reading from somebody with nothing to say.
 */
function readConversation(value: unknown): ConversationView {
  if (!isRecord(value)) return CONVERSATION_NONE;
  const { subject, speaker, greeting, action, outcome, code, message, residue, handoff, topic } = value;
  if (
    typeof subject !== 'string' ||
    typeof speaker !== 'string' ||
    typeof greeting !== 'string' ||
    typeof action !== 'string' ||
    typeof outcome !== 'string' ||
    typeof code !== 'string' ||
    typeof message !== 'string' ||
    typeof residue !== 'string' ||
    typeof handoff !== 'string' ||
    typeof topic !== 'string'
  ) {
    return CONVERSATION_NONE;
  }

  const people = readList(value.people, (entry) => {
    const { id, name, portrait } = entry;
    if (typeof id !== 'string' || typeof name !== 'string' || typeof portrait !== 'string') return null;
    return { id, name, portrait, speaking: entry.speaking === true };
  });
  const readTopics = (entries: unknown): ConversationTopicView[] =>
    readList(entries, (entry) => {
      const { id, label, reason } = entry;
      if (typeof id !== 'string' || typeof label !== 'string' || typeof reason !== 'string') return null;
      return { id, label, available: entry.available === true, reason };
    });
  const said = readList(value.said, (entry) => {
    const { speaker: who, text, residue: left } = entry;
    if (typeof who !== 'string' || typeof text !== 'string' || typeof left !== 'string') return null;
    return { speaker: who, text, residue: left };
  });

  return {
    available: value.available === true,
    open: value.open === true,
    subject,
    speaker,
    greeting,
    people,
    topics: readTopics(value.topics),
    withheld: readTopics(value.withheld),
    said,
    action,
    outcome,
    code,
    message,
    residue,
    handoff,
    topic,
  };
}

/**
 * Reads one actor of a fight: the fight's own facts, or null when the entry is not one. A readiness that is
 * not published is not assumed ready — a fighter the panel cannot read is one it will not offer to act with.
 */
function readFighter(value: unknown): FighterView | null {
  if (!isRecord(value)) return null;
  const { id, name } = value;
  if (typeof id !== 'string' || typeof name !== 'string') return null;
  return {
    id,
    name,
    ready: value.ready === true,
    recoverySeconds: typeof value.recoverySeconds === 'number' ? value.recoverySeconds : 0,
    distance: typeof value.distance === 'number' ? value.distance : 0,
    hitPoints: typeof value.hitPoints === 'number' ? value.hitPoints : 0,
    hitPointsMax: typeof value.hitPointsMax === 'number' ? value.hitPointsMax : 0,
    conditions: typeof value.conditions === 'string' ? value.conditions : '',
    down: value.down === true,
    activity: typeof value.activity === 'string' ? value.activity : '',
  };
}

/**
 * Reads one actor's place in the round's order, or null when the block does not describe one. Every fact is
 * the product's own; a row this companion cannot read is dropped rather than invented.
 */
function readTurnOrder(value: unknown): TurnOrderView | null {
  if (!isRecord(value)) return null;
  const { id, name, side } = value;
  if (typeof id !== 'string' || typeof name !== 'string' || typeof side !== 'string') return null;
  const number = (entry: unknown): number => (typeof entry === 'number' ? entry : 0);
  return {
    id,
    name,
    side,
    remainingSeconds: number(value.remainingSeconds),
    ready: value.ready === true,
    canAct: value.canAct !== false,
    waiting: value.waiting === true,
    current: value.current === true,
  };
}

/** Reads the round a paced fight is in, or the no-round value when the block carries none. */
function readTurn(value: unknown): TurnView {
  if (!isRecord(value)) return TURN_NONE;
  const { phase, actor, actorName, last } = value;
  if (
    typeof phase !== 'string' ||
    typeof actor !== 'string' ||
    typeof actorName !== 'string' ||
    typeof last !== 'string'
  ) {
    return TURN_NONE;
  }

  const number = (entry: unknown): number => (typeof entry === 'number' ? entry : 0);
  const order = Array.isArray(value.order)
    ? value.order.map(readTurnOrder).filter((entry): entry is TurnOrderView => entry !== null)
    : [];
  return {
    phase,
    round: number(value.round),
    actor,
    actorName,
    playerTurn: value.playerTurn === true,
    dueSeconds: number(value.dueSeconds),
    roundSeconds: number(value.roundSeconds),
    elapsedSeconds: number(value.elapsedSeconds),
    movementSeconds: number(value.movementSeconds),
    last,
    order,
  };
}

function readCombat(value: unknown): CombatView {
  if (!isRecord(value)) return COMBAT_NONE;
  const { actor, kind, target, outcome, code, message } = value;
  if (
    typeof actor !== 'string' ||
    typeof kind !== 'string' ||
    typeof target !== 'string' ||
    typeof outcome !== 'string' ||
    typeof code !== 'string' ||
    typeof message !== 'string'
  ) {
    return COMBAT_NONE;
  }

  const fighters = (entries: unknown): FighterView[] =>
    Array.isArray(entries) ? entries.map(readFighter).filter((entry): entry is FighterView => entry !== null) : [];
  const number = (entry: unknown): number => (typeof entry === 'number' ? entry : 0);
  return {
    available: value.available === true,
    engaged: value.engaged === true,
    opposition: number(value.opposition),
    ready: number(value.ready),
    members: fighters(value.members),
    enemies: fighters(value.enemies),
    actor,
    kind,
    target,
    outcome,
    code,
    message,
    recoverySeconds: number(value.recoverySeconds),
    resolved: value.resolved === true,
    hit: value.hit === true,
    chance: number(value.chance),
    damageRolled: number(value.damageRolled),
    damage: number(value.damage),
    damageKind: typeof value.damageKind === 'string' ? value.damageKind : '',
    resistance: typeof value.resistance === 'string' ? value.resistance : '',
    condition: typeof value.condition === 'string' ? value.condition : '',
    targetDown: value.targetDown === true,
    byParty: value.byParty === true,
    pacing: typeof value.pacing === 'string' ? value.pacing : 'realtime',
    turn: readTurn(value.turn),
  };
}

function readRest(value: unknown): RestView {
  if (!isRecord(value)) return REST_NONE;
  const { kind, outcome, code, message, from, to, unit, cleared, shortage, fatigueDue } = value;
  if (
    typeof kind !== 'string' ||
    typeof outcome !== 'string' ||
    typeof code !== 'string' ||
    typeof message !== 'string' ||
    typeof from !== 'string' ||
    typeof to !== 'string' ||
    typeof unit !== 'string' ||
    typeof cleared !== 'string' ||
    typeof shortage !== 'string' ||
    typeof fatigueDue !== 'string'
  ) {
    return REST_NONE;
  }

  const number = (entry: unknown): number => (typeof entry === 'number' ? entry : 0);
  return {
    available: value.available === true,
    kind,
    outcome,
    code,
    message,
    from,
    to,
    elapsedSeconds: number(value.elapsedSeconds),
    charged: number(value.charged),
    covered: number(value.covered),
    unit,
    interrupted: value.interrupted === true,
    recovered: value.recovered === true,
    restored: number(value.restored),
    cleared,
    shortage,
    tired: value.tired === true,
    fatigueDue,
    fatigueLanded: number(value.fatigueLanded),
  };
}

function readCreation(value: unknown): CreationView {
  if (!isRecord(value)) return CREATION_NONE;
  const { member, members, step, pool, refusalCode, refusalMessage } = value;
  if (
    typeof member !== 'number' ||
    typeof members !== 'number' ||
    typeof step !== 'string' ||
    typeof pool !== 'number' ||
    typeof refusalCode !== 'string' ||
    typeof refusalMessage !== 'string'
  ) {
    return CREATION_NONE;
  }

  return {
    active: value.active === true,
    accepted: value.accepted === true,
    hasDefault: value.hasDefault === true,
    member,
    members,
    step,
    pool,
    refusalCode,
    refusalMessage,
    roster: readList(value.roster, (entry) =>
      typeof entry.index === 'number' &&
      typeof entry.step === 'string' &&
      typeof entry.name === 'string' &&
      typeof entry.race === 'string' &&
      typeof entry.class === 'string' &&
      typeof entry.portrait === 'string' &&
      typeof entry.pool === 'number'
        ? {
            index: entry.index,
            step: entry.step,
            name: entry.name,
            race: entry.race,
            class: entry.class,
            portrait: entry.portrait,
            pool: entry.pool,
          }
        : null),
    portraits: readList(value.portraits, (entry) =>
      typeof entry.id === 'string' && typeof entry.name === 'string' && typeof entry.race === 'string'
        ? { id: entry.id, name: entry.name, race: entry.race, selected: entry.selected === true }
        : null),
    classes: readList(value.classes, (entry) =>
      typeof entry.id === 'string' && typeof entry.name === 'string'
        ? { id: entry.id, name: entry.name, selected: entry.selected === true }
        : null),
    skills: readList(value.skills, (entry) =>
      typeof entry.id === 'string' && typeof entry.name === 'string' && typeof entry.state === 'string'
        ? { id: entry.id, name: entry.name, state: entry.state }
        : null),
    attributes: readList(value.attributes, (entry) =>
      typeof entry.id === 'string' &&
      typeof entry.name === 'string' &&
      typeof entry.value === 'number' &&
      typeof entry.minimum === 'number' &&
      typeof entry.maximum === 'number'
        ? {
            id: entry.id,
            name: entry.name,
            value: entry.value,
            minimum: entry.minimum,
            maximum: entry.maximum,
            canRaise: entry.canRaise === true,
            canLower: entry.canLower === true,
          }
        : null),
    party: readList(value.party, (entry) =>
      typeof entry.index === 'number' &&
      typeof entry.name === 'string' &&
      typeof entry.race === 'string' &&
      typeof entry.class === 'string' &&
      typeof entry.portrait === 'string'
        ? {
            index: entry.index,
            name: entry.name,
            race: entry.race,
            class: entry.class,
            portrait: entry.portrait,
          }
        : null),
  };
}

function readSnapshot(value: unknown): SnapshotView | null {
  if (!isRecord(value)) return null;
  const composition = value.composition;
  const session = value.session;
  // A projection that does not carry a session is not one this companion understands, and rendering
  // half of it would show a panel that disagrees with the product.
  if (!isRecord(composition) || !isRecord(session)) return null;

  // A projection with no world, though, is not a broken projection: a session whose content has not
  // been generated yet has no places, and the panel says so rather than refusing to render at all.
  const world = isRecord(value.world) ? value.world : {};
  // Movement facts are optional in the same way: a session that has never moved has none to publish,
  // and the panel shows that it does not know rather than that the way is clear.
  const movement = isRecord(value.movement) ? value.movement : {};
  // The clock and the party are optional on the same terms: a ruleset that composed neither publishes
  // blocks that say so, and a projection that carries none at all still describes a session worth showing.
  const clock = readClock(value.clock);
  const party = readParty(value.party);
  const creation = readCreation(value.creation);
  const save = readSave(value.save);
  const interaction = readInteraction(value.interaction);
  const service = readService(value.service);
  const rest = readRest(value.rest);
  const conversation = readConversation(value.conversation);
  const combat = readCombat(value.combat);
  const progression = readProgression(value.progression);
  const skills = readSkills(value.skills);
  const magic = readMagic(value.magic);
  const { ruleset, title, bundle, contentPacks } = composition;
  const { mode, simulationSeconds, admittedSteps, updates } = session;
  if (
    typeof ruleset !== 'string' ||
    typeof title !== 'string' ||
    typeof bundle !== 'string' ||
    typeof contentPacks !== 'number' ||
    typeof mode !== 'string' ||
    typeof simulationSeconds !== 'number' ||
    typeof admittedSteps !== 'number' ||
    typeof updates !== 'number'
  ) {
    return null;
  }
  const place = world.place ?? '';
  const placeName = world.name ?? '';
  const kind = world.kind ?? '';
  const x = world.x ?? 0;
  const y = world.y ?? 0;
  const z = world.z ?? 0;
  const yaw = world.yaw ?? 0;
  const visited = world.visited ?? 0;
  const places = world.places ?? 0;
  // The place's own hours are read the same way the clock's are: a projection that carries none describes a
  // place this companion cannot read as clocked, and the panel shows that rather than inventing a window.
  const open = world.open === true;
  const hours = world.hours ?? '';
  const nextChange = world.nextChange ?? '';
  const motion = movement.motion ?? 'none';
  const blocked = movement.blocked ?? 'none';
  const stepRise = movement.stepRise ?? 0;
  const fallDistance = movement.fallDistance ?? 0;
  const fallDamage = movement.fallDamage ?? 0;
  if (
    typeof place !== 'string' ||
    typeof placeName !== 'string' ||
    typeof kind !== 'string' ||
    typeof x !== 'number' ||
    typeof y !== 'number' ||
    typeof z !== 'number' ||
    typeof yaw !== 'number' ||
    typeof visited !== 'number' ||
    typeof places !== 'number' ||
    typeof hours !== 'string' ||
    typeof nextChange !== 'string' ||
    typeof motion !== 'string' ||
    typeof blocked !== 'string' ||
    typeof stepRise !== 'number' ||
    typeof fallDistance !== 'number' ||
    typeof fallDamage !== 'number'
  ) {
    return null;
  }

  return {
    composition: { ruleset, title, bundle, contentPacks },
    session: { mode, simulationSeconds, admittedSteps, updates },
    world: { place, name: placeName, kind, x, y, z, yaw, visited, places, open, hours, nextChange },
    movement: { motion, blocked, stepRise, fallDistance, fallDamage },
    clock,
    party,
    creation,
    save,
    interaction,
    service,
    rest,
    conversation,
    combat,
    progression,
    skills,
    magic,
  };
}

/**
 * Mounts the companion into `root` and returns a disposer. The returned object holds the only
 * subscription this module creates; disposing it leaves no listener behind.
 */
export function mountProductUi(root: HTMLElement, context: ProductUiContext): { dispose(): void } {
  const style = document.createElement('style');
  style.textContent = STYLES;

  const panel = document.createElement('section');
  panel.className = 'crawler-session';
  panel.dataset.mode = 'starting';
  // Nothing has moved yet, so the panel's own state says it has no movement facts rather than that
  // nothing stopped the party.
  panel.dataset.motion = 'none';
  panel.dataset.blocked = 'none';
  panel.dataset.clock = 'none';
  panel.dataset.party = 'none';
  panel.dataset.light = 'unknown';
  panel.dataset.creation = 'none';
  panel.dataset.save = 'none';
  panel.dataset.interaction = 'none';
  panel.dataset.use = 'none';
  panel.dataset.service = 'none';
  panel.dataset.serviceState = 'unknown';
  panel.dataset.serviceAction = '';
  panel.dataset.serviceOutcome = 'none';
  panel.dataset.conversation = 'none';
  panel.dataset.conversationAction = '';
  panel.dataset.conversationOutcome = 'none';
  panel.dataset.progression = 'none';
  panel.dataset.progressionOutcome = 'none';
  panel.dataset.rest = 'none';
  panel.dataset.restAction = '';
  panel.dataset.restOutcome = 'none';
  panel.dataset.tired = 'no';

  const title = document.createElement('h1');
  const ruleset = document.createElement('p');
  ruleset.className = 'crawler-ruleset';
  const bundle = document.createElement('p');
  bundle.className = 'crawler-bundle';
  const place = document.createElement('p');
  place.className = 'crawler-place';

  const details = document.createElement('dl');
  const rows: Record<string, HTMLElement> = {};
  for (const [key, label] of [
    ['mode', 'Session'],
    ['simulation', 'Simulation'],
    ['steps', 'Admitted steps'],
    ['updates', 'Updates'],
    ['content', 'Content'],
    ['date', 'Date'],
    ['time', 'Time'],
    ['days', 'Days'],
    ['party', 'Party'],
    ['pack', 'Pack'],
    ['coins', 'Coins'],
    ['food', 'Food'],
    ['standing', 'Standing'],
    ['condition', 'Condition'],
    ['vitality', 'Vitality'],
    ['magic', 'Magic'],
    ['place', 'Place'],
    ['hours', 'Hours'],
    ['pose', 'Position'],
    ['explored', 'Explored'],
    ['motion', 'Motion'],
    ['blocked', 'Blocked'],
    ['step', 'Step up'],
    ['fall', 'Fall'],
    ['member', 'Member'],
    ['creationStep', 'Creation step'],
    ['pool', 'Pool'],
    ['save', 'Save'],
    ['start', 'Start'],
    ['facing', 'Facing'],
    ['bodies', 'Bodies here'],
    ['requires', 'Requires'],
    ['use', 'Use'],
  ] as const) {
    const term = document.createElement('dt');
    term.textContent = label;
    const value = document.createElement('dd');
    value.textContent = '—';
    details.append(term, value);
    rows[key] = value;
  }

  const action = document.createElement('button');
  action.type = 'button';
  action.disabled = true;
  action.textContent = 'Starting…';

  // The save control: one button that asks for a save now, and the outcome of the last request. The
  // button is disabled while there is nothing to save — during creation and before the session runs — and
  // while the session has no store to write to; asking anyway is possible with the key, and the answer is
  // then shown here rather than swallowed.
  const saveButton = document.createElement('button');
  saveButton.type = 'button';
  saveButton.className = 'crawler-save';
  saveButton.disabled = true;
  saveButton.textContent = 'Save session';

  const saveResult = document.createElement('p');
  saveResult.className = 'crawler-save-result';
  saveResult.hidden = true;

  // The use control: one button that uses whatever the party faces, and the outcome of the last use. It is
  // disabled while the session holds no interaction — during creation, or in a session whose ruleset
  // answered none — and while nothing is focused, because a button that cannot work must not look like one
  // that can; the reason the reticle holds nothing is on the Facing row beside it.
  const useButton = document.createElement('button');
  useButton.type = 'button';
  useButton.className = 'crawler-use';
  useButton.disabled = true;
  useButton.textContent = 'Use';

  const useResult = document.createElement('p');
  useResult.className = 'crawler-use-result';
  useResult.hidden = true;
  const useResidue = document.createElement('p');
  useResidue.className = 'crawler-use-residue';
  useResidue.hidden = true;

  const hint = document.createElement('p');
  hint.className = 'crawler-hint';
  hint.textContent = 'Pause or resume with the button, or with the P key.';

  // The creation screen: the members as they stand, the choices creation offers the member being made,
  // the rule the last choice broke, and the party once one has been accepted. Every list is rebuilt from
  // the projection, so the screen holds nothing the product did not publish.
  const creation = document.createElement('section');
  creation.className = 'crawler-creation';
  creation.hidden = true;
  const stepHead = document.createElement('p');
  stepHead.className = 'crawler-step-head';
  const memberList = document.createElement('div');
  memberList.className = 'crawler-row';
  const portraitRow = document.createElement('div');
  portraitRow.className = 'crawler-row';
  const classRow = document.createElement('div');
  classRow.className = 'crawler-row';
  const skillRow = document.createElement('div');
  skillRow.className = 'crawler-row';
  const attributeRow = document.createElement('div');
  attributeRow.className = 'crawler-row';
  const nameRow = document.createElement('div');
  nameRow.className = 'crawler-name';
  const nameInput = document.createElement('input');
  nameInput.type = 'text';
  nameInput.placeholder = 'Name this character';
  const nameButton = document.createElement('button');
  nameButton.type = 'button';
  nameButton.textContent = 'Set name';
  nameRow.append(nameInput, nameButton);
  const flowRow = document.createElement('div');
  flowRow.className = 'crawler-actions';
  const advanceButton = document.createElement('button');
  advanceButton.type = 'button';
  advanceButton.textContent = 'Confirm step';
  const acceptButton = document.createElement('button');
  acceptButton.type = 'button';
  acceptButton.textContent = 'Accept party';
  flowRow.append(advanceButton, acceptButton);
  const refusal = document.createElement('p');
  refusal.className = 'crawler-refusal';
  refusal.hidden = true;
  const acceptedList = document.createElement('ul');
  acceptedList.className = 'crawler-accepted';
  creation.append(
    stepHead,
    memberList,
    portraitRow,
    classRow,
    skillRow,
    attributeRow,
    nameRow,
    flowRow,
    refusal,
    acceptedList,
  );

  // The conversation screen: who the party is speaking with, what they said, what can be brought up, and
  // what the state withholds. It comes before the counter's screen because it is what a player reaches a
  // counter through: a person is talked to first, and stepping up to what they keep is one of their offers.
  const conversation = document.createElement('section');
  conversation.className = 'crawler-conversation';
  conversation.hidden = true;
  const conversationHead = document.createElement('p');
  conversationHead.className = 'crawler-step-head';
  const conversationGreeting = document.createElement('p');
  conversationGreeting.className = 'crawler-conversation-greeting';
  const conversationPeople = document.createElement('div');
  conversationPeople.className = 'crawler-row';
  const conversationTopics = document.createElement('div');
  conversationTopics.className = 'crawler-options';
  const conversationWithheld = document.createElement('ul');
  conversationWithheld.className = 'crawler-withheld';
  const conversationSaid = document.createElement('ul');
  conversationSaid.className = 'crawler-said';
  const conversationActions = document.createElement('div');
  conversationActions.className = 'crawler-actions';
  const conversationLeave = document.createElement('button');
  conversationLeave.type = 'button';
  conversationLeave.dataset.id = ACTION_CONVERSATION_LEAVE;
  conversationLeave.textContent = 'Take your leave';
  conversationLeave.addEventListener('click', () => claim(ACTION_CONVERSATION_LEAVE));
  conversationActions.append(conversationLeave);
  const conversationResult = document.createElement('p');
  conversationResult.className = 'crawler-conversation-result';
  conversationResult.hidden = true;
  const conversationResidue = document.createElement('p');
  conversationResidue.className = 'crawler-conversation-residue';
  conversationResidue.hidden = true;
  conversation.append(
    conversationHead,
    conversationGreeting,
    conversationPeople,
    conversationTopics,
    conversationWithheld,
    conversationSaid,
    conversationActions,
    conversationResult,
    conversationResidue,
  );

  // The service screen: the counter the party stands at, what its shelves hold and at what price, what it
  // teaches, what it would buy, and what the last command did. Every list is rebuilt from the projection,
  // so the screen holds nothing the product did not publish and decides no price itself.
  const service = document.createElement('section');
  service.className = 'crawler-service';
  service.hidden = true;
  const serviceHead = document.createElement('p');
  serviceHead.className = 'crawler-step-head';
  const serviceState = document.createElement('p');
  serviceState.className = 'crawler-service-state';
  const serviceAccess = document.createElement('p');
  serviceAccess.className = 'crawler-service-access';
  const serviceMemberRow = document.createElement('div');
  serviceMemberRow.className = 'crawler-service-member';
  const serviceMemberLabel = document.createElement('span');
  serviceMemberLabel.className = 'crawler-row-label';
  serviceMemberLabel.textContent = 'Lesson goes to';
  const serviceMemberSelect = document.createElement('select');
  serviceMemberRow.append(serviceMemberLabel, serviceMemberSelect);
  const stockRow = document.createElement('div');
  stockRow.className = 'crawler-row';
  const saleRow = document.createElement('div');
  saleRow.className = 'crawler-row';
  const lessonRow = document.createElement('div');
  lessonRow.className = 'crawler-row';
  const serviceResult = document.createElement('p');
  serviceResult.className = 'crawler-service-result';
  serviceResult.hidden = true;
  const serviceActions = document.createElement('div');
  serviceActions.className = 'crawler-actions';
  const serviceLeave = document.createElement('button');
  serviceLeave.type = 'button';
  serviceLeave.textContent = 'Leave the counter';
  serviceActions.append(serviceLeave);
  service.append(serviceHead, serviceState, serviceAccess, serviceMemberRow, stockRow, saleRow, lessonRow, serviceActions, serviceResult);

  // What each member has earned and what a level would cost: the level, the experience banked against the
  // curve the ruleset states, the points held, and the fee the counter the party stands at would charge.
  // The train control is offered per member and only when a counter here trains them, so a button that
  // cannot work is not shown; every number is the product's own.
  const progression = document.createElement('section');
  progression.className = 'crawler-progression';
  progression.hidden = true;
  const progressionHead = document.createElement('p');
  progressionHead.className = 'crawler-step-head';
  progressionHead.textContent = 'Experience and levels';
  const progressionState = document.createElement('p');
  progressionState.className = 'crawler-progression-state';
  const progressionMembers = document.createElement('div');
  progressionMembers.className = 'crawler-progression-members';
  const progressionResult = document.createElement('p');
  progressionResult.className = 'crawler-progression-result';
  progressionResult.hidden = true;
  progression.append(progressionHead, progressionState, progressionMembers, progressionResult);

  // The skills sit under the levels they were bought with: each member's own rows, the ceiling their class
  // and rank impose, and one control per row that raises it. The panel prints the price and the refusal the
  // product worked out and raises nothing itself, which is what keeps one spend path in the product rather
  // than one per screen.
  const skills = document.createElement('section');
  skills.className = 'crawler-skills';
  skills.hidden = true;
  const skillsHead = document.createElement('p');
  skillsHead.className = 'crawler-step-head';
  skillsHead.textContent = 'Skills';
  const skillsState = document.createElement('p');
  skillsState.className = 'crawler-skills-state';
  const skillsMembers = document.createElement('div');
  skillsMembers.className = 'crawler-skills-members';
  const skillsResult = document.createElement('p');
  skillsResult.className = 'crawler-skills-result';
  skillsResult.hidden = true;
  skills.append(skillsHead, skillsState, skillsMembers, skillsResult);

  // The spellbook: each member's own spells with what casting one costs that caster, the actors a casting
  // may be aimed at, and the answer the last casting got. Nothing here is computed: the price, the rung, the
  // aim, and the target list are the product's own published answers, and the panel's buttons name the row
  // a player pressed.
  const magic = document.createElement('section');
  magic.className = 'crawler-magic';
  magic.hidden = true;
  const magicHead = document.createElement('p');
  magicHead.className = 'crawler-step-head';
  magicHead.textContent = 'Spellbook';
  const magicState = document.createElement('p');
  magicState.className = 'crawler-magic-state';
  const magicMembers = document.createElement('div');
  magicMembers.className = 'crawler-magic-members';
  const magicResult = document.createElement('p');
  magicResult.className = 'crawler-magic-result';
  magicResult.hidden = true;
  const magicFacts = document.createElement('ul');
  magicFacts.className = 'crawler-magic-facts';
  magicFacts.hidden = true;
  const magicRunning = document.createElement('p');
  magicRunning.className = 'crawler-magic-running';
  magicRunning.hidden = true;
  magic.append(magicHead, magicState, magicMembers, magicResult, magicFacts, magicRunning);

  // The stop controls: one button per act, and the answer the last one got. A rest heals and a wait does
  // not, so the buttons are never collapsed into one; and the fatigue line is the clock's own deadline,
  // which is why a player can see when the party next needs to sleep.
  const rest = document.createElement('section');
  rest.className = 'crawler-rest';
  rest.hidden = true;
  const restHead = document.createElement('p');
  restHead.className = 'crawler-step-head';
  restHead.textContent = 'Rest, camp, and wait';
  const restState = document.createElement('p');
  restState.className = 'crawler-rest-state';
  const restActions = document.createElement('div');
  restActions.className = 'crawler-actions';
  const restButtons: { readonly id: string; readonly label: string }[] = [
    { id: ACTION_REST, label: 'Rest & heal 8 hours' },
    { id: ACTION_CAMP, label: 'Make camp' },
    { id: ACTION_WAIT_DAWN, label: 'Wait until dawn' },
    { id: ACTION_WAIT_HOUR, label: 'Wait an hour' },
    { id: ACTION_WAIT_FIVE_MINUTES, label: 'Wait 5 minutes' },
  ];
  for (const entry of restButtons) {
    const button = document.createElement('button');
    button.type = 'button';
    button.dataset.id = entry.id;
    button.textContent = entry.label;
    button.addEventListener('click', () => claim(entry.id));
    restActions.append(button);
  }

  const restResult = document.createElement('p');
  restResult.className = 'crawler-rest-result';
  restResult.hidden = true;
  rest.append(restHead, restState, restActions, restResult);

  // The fight sits beside the stop controls: it is what a player acts on while something is hostile, and it
  // is shown whether or not anything is — a session with no fight mechanism, a quiet place, and a fight in
  // progress are three different facts. The attack button is disabled while no member may act, which is the
  // product's own readiness read off the projection rather than a countdown this screen runs.
  const combat = document.createElement('section');
  combat.className = 'crawler-combat';
  combat.hidden = true;
  const combatHead = document.createElement('p');
  combatHead.className = 'crawler-step-head';
  combatHead.textContent = 'Fight';
  const combatState = document.createElement('p');
  combatState.className = 'crawler-combat-state';
  // Which pacing the one fight is being played in, and the round it is in: whose turn it is and what that
  // actor still owes, printed from the product's own numbers rather than counted down here.
  const combatTurn = document.createElement('p');
  combatTurn.className = 'crawler-combat-turn';
  const combatActions = document.createElement('div');
  combatActions.className = 'crawler-actions';
  const attackButton = document.createElement('button');
  attackButton.type = 'button';
  attackButton.className = 'crawler-attack';
  attackButton.textContent = 'Attack';
  // Switching the pacing is its own control because it is its own act: the fight is the same fight either
  // way, and a player needs a way to say which pacing they want it played in.
  const paceButton = document.createElement('button');
  paceButton.type = 'button';
  paceButton.className = 'crawler-pace';
  paceButton.textContent = 'Turn-based';
  // Skipping and waiting are separate controls because their consequences differ: a skipped turn forfeits
  // the round and owes the action it did not take, and a waited turn is deferred to the round's end.
  const skipButton = document.createElement('button');
  skipButton.type = 'button';
  skipButton.className = 'crawler-skip';
  skipButton.textContent = 'Skip';
  const waitButton = document.createElement('button');
  waitButton.type = 'button';
  waitButton.className = 'crawler-wait';
  waitButton.textContent = 'Wait';
  combatActions.append(attackButton, paceButton, skipButton, waitButton);
  const combatMembers = document.createElement('ul');
  combatMembers.className = 'crawler-combat-members';
  const combatEnemies = document.createElement('ul');
  combatEnemies.className = 'crawler-combat-enemies';
  // The order the round will act in, as the fight itself reads it: the same recovery that paces real time,
  // so a player can see whose turn is coming without the screen keeping an initiative of its own.
  const combatOrder = document.createElement('ul');
  combatOrder.className = 'crawler-turn-order';
  const combatResult = document.createElement('p');
  combatResult.className = 'crawler-combat-result';
  combatResult.hidden = true;
  combat.append(
    combatHead,
    combatState,
    combatTurn,
    combatActions,
    combatMembers,
    combatEnemies,
    combatOrder,
    combatResult,
  );

  // The creation screen comes before the long list of facts below: while a party is being made its
  // choices are what a player acts on, and a screen whose controls sat below twenty rows of values would
  // put them off the bottom of a short window. The service screen sits beside it for the same reason: a
  // counter's shelves are what a player acts on, not another twenty rows of values.
  panel.append(
    title,
    ruleset,
    bundle,
    place,
    creation,
    conversation,
    service,
    rest,
    combat,
    progression,
    skills,
    magic,
    details,
    action,
    saveButton,
    useButton,
    saveResult,
    useResult,
    useResidue,
    hint,
  );
  root.append(style, panel);

  let current = 'starting';
  let renderedConversation = '';
  let renderedProgression = '';
  let renderedSkills = '';
  let renderedMagic = '';
  const claim = (name: string, data: Record<string, unknown> = {}): void => {
    context.intents?.claim(UI_ACTION_INTENT, {
      kind: 'product-payload',
      contract: UI_ACTION_CONTRACT,
      data: { action: name, ...data },
    });
  };

  action.addEventListener('click', () => {
    // A paced fight is a running session that happens to be waiting for a turn, so the pause control is the
    // one it has everywhere else.
    if (current === 'running' || current === 'turnbased') claim(ACTION_PAUSE);
    else if (current === 'paused') claim(ACTION_RESUME);
  });

  saveButton.addEventListener('click', () => claim(ACTION_SAVE));
  attackButton.addEventListener('click', () => claim(ACTION_ATTACK));
  paceButton.addEventListener('click', () => claim(ACTION_TURN_BASED));
  skipButton.addEventListener('click', () => claim(ACTION_TURN_SKIP));
  waitButton.addEventListener('click', () => claim(ACTION_TURN_WAIT));
  serviceLeave.addEventListener('click', () => claim(ACTION_SERVICE_LEAVE));
  useButton.addEventListener('click', () => claim(ACTION_USE));
  advanceButton.addEventListener('click', () => claim(ACTION_ADVANCE));
  acceptButton.addEventListener('click', () => claim(ACTION_ACCEPT));
  nameButton.addEventListener('click', () => claim(ACTION_SET_NAME, { name: nameInput.value }));

  /** The parameter a choice action carries, so the action name and its payload field stay in step. */
  const parameter = (name: string): string => {
    switch (name) {
      case ACTION_SELECT_PORTRAIT:
        return 'portrait';
      case ACTION_SELECT_CLASS:
        return 'class';
      case ACTION_CHOOSE_SKILL:
      case ACTION_REMOVE_SKILL:
        return 'skill';
      case ACTION_RAISE_ATTRIBUTE:
      case ACTION_LOWER_ATTRIBUTE:
        return 'attribute';
      default:
        return 'id';
    }
  };

  /** One labelled row of options, each a button that reports the choice it names. */
  const options = (
    label: string,
    entries: readonly {
      readonly id: string;
      readonly text: string;
      readonly selected?: boolean;
      readonly available?: boolean;
      readonly choice?: string;
    }[],
  ): HTMLElement => {
    const row = document.createElement('div');
    const heading = document.createElement('span');
    heading.className = 'crawler-row-label';
    heading.textContent = label;
    const list = document.createElement('div');
    list.className = 'crawler-options';
    for (const entry of entries) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = entry.text;
      button.dataset.id = entry.id;
      if (entry.selected !== undefined) button.dataset.selected = String(entry.selected);
      if (entry.available !== undefined) button.dataset.available = String(entry.available);
      // A choice the projection lists is offered whatever its state: the flow is what decides whether it
      // is legal, and its refusal is what the screen shows. Hiding a choice here would hide the rule.
      if (entry.choice !== undefined) {
        const field = parameter(entry.choice);
        button.addEventListener('click', () => claim(entry.choice!, { [field]: entry.id }));
      } else {
        button.disabled = true;
      }

      list.append(button);
    }

    row.append(heading, list);
    return row;
  };

  /** The members as they stand, each a button that moves creation onto it. */
  const members = (view: CreationView): HTMLElement => {
    const row = document.createElement('div');
    const heading = document.createElement('span');
    heading.className = 'crawler-row-label';
    heading.textContent = `Party — member ${view.member + 1} of ${view.members}`;
    const list = document.createElement('div');
    list.className = 'crawler-options';
    for (const member of view.roster) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = describe(member);
      button.dataset.member = String(member.index);
      button.dataset.step = member.step;
      button.dataset.selected = String(member.index === view.member);
      button.addEventListener('click', () => claim(ACTION_SELECT_MEMBER, { member: member.index }));
      list.append(button);
    }

    row.append(heading, list);
    return row;
  };

  /** The attributes with the two moves creation allows, each carrying what the race permits. */
  const attributes = (view: CreationView): HTMLElement => {
    const row = document.createElement('div');
    const heading = document.createElement('span');
    heading.className = 'crawler-row-label';
    heading.textContent = `Attributes — ${view.pool} point${view.pool === 1 ? '' : 's'} left`;
    row.append(heading);
    for (const attribute of view.attributes) {
      const line = document.createElement('div');
      line.className = 'crawler-attribute';
      const value = document.createElement('span');
      value.textContent = `${attribute.name} ${attribute.value} (${attribute.minimum}–${attribute.maximum})`;
      const lower = document.createElement('button');
      lower.type = 'button';
      lower.textContent = '−';
      lower.dataset.attribute = attribute.id;
      lower.dataset.canLower = String(attribute.canLower);
      lower.addEventListener('click', () => claim(ACTION_LOWER_ATTRIBUTE, { attribute: attribute.id }));
      const raise = document.createElement('button');
      raise.type = 'button';
      raise.textContent = '+';
      raise.dataset.attribute = attribute.id;
      raise.dataset.canRaise = String(attribute.canRaise);
      raise.addEventListener('click', () => claim(ACTION_RAISE_ATTRIBUTE, { attribute: attribute.id }));
      line.append(value, lower, raise);
      row.append(line);
    }

    return row;
  };

  /**
   * One member of the party as a person reads it, from the values the flow published and no others. The
   * race is left to the chosen portrait and to the accepted list: it is the portrait that decided it, and
   * a member button that named it too would wrap onto a line of its own in a short window.
   */
  const describe = (member: CreationMemberView): string => {
    const named = member.name === '' ? 'unnamed' : member.name;
    return `${member.index + 1}. ${named} · ${member.class === '' ? 'no class' : member.class} · ${member.step}`;
  };

  /**
   * Renders the creation screen. The choices are rebuilt only when what they carry changed, because the
   * product publishes an update for every admitted step and rebuilding a screen sixty times a second
   * would fight the pointer for no reason: what changed is what is redrawn.
   */
  let renderedChoices = '';
  const renderCreation = (view: CreationView, resumed: boolean): void => {
    // A resumed session plays a party it did not create here, and says so rather than claiming a creation
    // that never happened: the roster below it is the same either way.
    panel.dataset.creation = view.active ? 'active' : view.accepted ? (resumed ? 'resumed' : 'accepted') : 'none';
    creation.hidden = !view.active && !view.accepted;
    refusal.hidden = view.refusalMessage === '';
    refusal.dataset.code = view.refusalCode;
    refusal.textContent = view.refusalMessage;
    // What changed is what is redrawn: the product publishes an update for every admitted step, and
    // rebuilding a screen sixty times a second would fight the pointer for no reason. The refusal and the
    // mode are written every time because they are the two things a player must not miss.
    const signature = JSON.stringify(view);
    if (signature === renderedChoices) return;
    renderedChoices = signature;

    if (!view.active) {
      // The party a session accepted is shown from the party itself, so the screen's list is the party
      // being played rather than the draft that described it.
      acceptedList.replaceChildren(
        ...view.party.map((member) => {
          const item = document.createElement('li');
          item.textContent = `${member.index + 1}. ${member.name} · ${member.race} · ${member.class} · ${member.portrait}`;
          return item;
        }),
      );
      stepHead.textContent = view.accepted ? (resumed ? 'Party resumed' : 'Party accepted') : '';
      memberList.replaceChildren();
      portraitRow.replaceChildren();
      classRow.replaceChildren();
      skillRow.replaceChildren();
      attributeRow.replaceChildren();
      nameRow.hidden = true;
      flowRow.hidden = true;
      return;
    }

    acceptedList.replaceChildren();
    nameRow.hidden = false;
    flowRow.hidden = false;
    stepHead.textContent = view.roster.every((member) => member.step === 'complete')
      ? 'Every member is finished; accept the party, or reopen one to change it.'
      : `Creating member ${view.member + 1} of ${view.members} · ${view.step}`;
    memberList.replaceChildren(members(view));
    portraitRow.replaceChildren(
      options(
        'Portrait — decides the race',
        view.portraits.map((portrait) => ({
          id: portrait.id,
          text: portrait.name,
          selected: portrait.selected,
          choice: ACTION_SELECT_PORTRAIT,
        })),
      ),
    );
    classRow.replaceChildren(
      options(
        'Class — decides the skills',
        view.classes.map((option) => ({
          id: option.id,
          text: option.name,
          selected: option.selected,
          choice: ACTION_SELECT_CLASS,
        })),
      ),
    );
    skillRow.replaceChildren(
      options(
        'Skills — the fixed ones, the chosen ones, and the rest',
        view.skills.map((skill) => ({
          id: skill.id,
          text: skill.state === 'fixed' ? `${skill.name} (fixed)` : skill.name,
          selected: skill.state === 'chosen',
          available: skill.state === 'available',
          choice:
            skill.state === 'chosen'
              ? ACTION_REMOVE_SKILL
              : skill.state === 'available'
                ? ACTION_CHOOSE_SKILL
                : undefined,
        })),
      ),
    );
    attributeRow.replaceChildren(attributes(view));
  };

  /**
   * Renders the counter the party stands at. The lists are rebuilt only when what they carry changed,
   * because the product publishes an update for every admitted step and a shelf of buttons rebuilt sixty
   * times a second would fight the pointer for no reason; the verdict and the message are written every
   * time because they are what a player must not miss.
   */
  let renderedService = '';
  /**
   * Renders the conversation and the choices it offers. The offers are buttons that report the topic or the
   * person they name, and a topic the state withholds is shown as the reason it is withheld rather than as a
   * choice: the product would refuse it by name, and a button that cannot work must not look like one that
   * can. Every word here came from the product — the greeting, each topic, each reason, and what was said —
   * and the panel spells none of them itself.
   */
  const renderConversation = (view: ConversationView): void => {
    // A session with no mechanism, one that is speaking with nobody, and one whose last choice was refused
    // are three different facts: a panel that hid the section for the first would leave a product without
    // dialogue looking like a party that had simply not spoken to anybody.
    panel.dataset.conversation = !view.available ? 'none' : view.open ? 'open' : view.outcome === 'none' ? 'available' : 'closed';
    panel.dataset.conversationAction = view.action;
    panel.dataset.conversationOutcome = view.outcome;
    conversation.hidden = !view.available || (!view.open && view.outcome === 'none');
    conversationHead.textContent =
      view.speaker === ''
        ? ''
        : view.people.length > 1
          ? `Speaking with ${view.speaker} (${view.people.length} here)`
          : `Speaking with ${view.speaker}`;
    conversationGreeting.textContent = view.greeting;
    conversationResult.hidden = view.message === '';
    conversationResult.dataset.outcome = view.outcome;
    conversationResult.dataset.code = view.code;
    conversationResult.textContent = view.message;
    conversationResidue.hidden = view.residue === '';
    conversationResidue.textContent = view.residue;

    const signature = JSON.stringify(view);
    if (signature === renderedConversation) return;
    renderedConversation = signature;

    if (!view.open) {
      conversationPeople.replaceChildren();
      conversationTopics.replaceChildren();
      conversationWithheld.replaceChildren();
      conversationSaid.replaceChildren();
      return;
    }

    // Who is here: one button per person, which turns the conversation to them. The person speaking is
    // shown as the one already chosen, because a button that does nothing must not look like one that does.
    conversationPeople.replaceChildren(
      ...view.people.map((person) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.dataset.id = person.id;
        button.dataset.person = person.id;
        button.textContent = person.name;
        if (person.speaking) button.disabled = true;
        else button.addEventListener('click', () => claim(ACTION_CONVERSATION_PERSON, { target: person.id }));
        return button;
      }),
    );

    conversationTopics.replaceChildren(
      ...view.topics.map((topic) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.dataset.id = topic.id;
        button.textContent = topic.label;
        if (!topic.available) button.disabled = true;
        else button.addEventListener('click', () => claim(ACTION_CONVERSATION_TOPIC, { target: topic.id }));
        return button;
      }),
    );

    // What the state withholds, with the reason each is withheld: the same vocabulary a locked door uses,
    // so a player learns what the person is waiting for instead of seeing a shorter list.
    conversationWithheld.replaceChildren(
      ...view.withheld.map((topic) => {
        const item = document.createElement('li');
        item.dataset.id = topic.id;
        item.textContent = `${topic.label} — ${topic.reason}`;
        return item;
      }),
    );

    // What has been said while the conversation has been open, oldest first, so a line taken is still
    // readable after it leaves the list of things to bring up.
    conversationSaid.replaceChildren(
      ...view.said.map((line) => {
        const item = document.createElement('li');
        item.dataset.speaker = line.speaker;
        item.textContent = line.residue === '' ? line.text : `${line.text} ${line.residue}`;
        return item;
      }),
    );
  };

  const renderService = (view: ServiceView): void => {
    // A session with no mechanism, one that stands at no counter, one that is browsing, and one that was
    // turned away are four different facts: a panel that called the second 'closed' would show every player
    // walking the street as standing at a shut shop.
    const shown = view.available && (view.open || view.name !== '' || view.outcome !== 'none');
    panel.dataset.service = !view.available ? 'none' : view.open ? 'open' : shown ? 'closed' : 'away';
    panel.dataset.serviceState = view.state === '' ? 'unknown' : view.state;
    panel.dataset.serviceAction = view.action;
    panel.dataset.serviceOutcome = view.outcome;
    // A counter is shown while a visit is open, and also when the party was turned away from one: a shop
    // that is shut for the night, and the counter the party just left, both have something to say.
    service.hidden = !shown;
    serviceHead.textContent = view.name === '' ? '' : `${view.name}${view.proprietor === '' ? '' : ` · ${view.proprietor}`}`;
    serviceState.textContent =
      view.state === ''
        ? view.kind
        : `${view.kind}${view.kind === '' ? '' : ' · '}${view.state}${view.hours === '' ? '' : ` (${view.hours})`}`;
    serviceAccess.textContent = view.memberships.length === 0 ? '' : `Membership: ${view.memberships.join(', ')}`;
    serviceResult.hidden = view.message === '';
    serviceResult.dataset.outcome = view.outcome;
    serviceResult.dataset.code = view.code;
    serviceResult.textContent =
      view.message === ''
        ? ''
        : view.paid > 0
          ? `${view.message} Paid ${view.paid}; the purse holds ${view.coins}.`
          : view.earned > 0
            ? `${view.message} Received ${view.earned}; the purse holds ${view.coins}.`
            : view.message;

    const signature = JSON.stringify(view);
    if (signature === renderedService) return;
    renderedService = signature;

    if (!view.open) {
      stockRow.replaceChildren();
      saleRow.replaceChildren();
      lessonRow.replaceChildren();
      serviceMemberRow.hidden = true;
      return;
    }

    const member = serviceMemberSelect.value;
    serviceMemberSelect.replaceChildren(
      ...view.members.map((entry) => {
        const option = document.createElement('option');
        option.value = String(entry.index);
        option.textContent = entry.name;
        return option;
      }),
    );
    if (member !== '') serviceMemberSelect.value = member;
    serviceMemberRow.hidden = view.members.length < 2;

    /** One labelled row of offers, each a button that reports the command it names. */
    const offers = (
      label: string,
      entries: readonly {
        readonly id: string;
        readonly text: string;
        readonly action?: string;
        readonly available?: boolean;
        readonly payload?: () => Record<string, unknown>;
      }[],
    ): HTMLElement => {
      const row = document.createElement('div');
      const heading = document.createElement('span');
      heading.className = 'crawler-row-label';
      heading.textContent = label;
      const list = document.createElement('div');
      list.className = 'crawler-options';
      for (const entry of entries) {
        const button = document.createElement('button');
        button.type = 'button';
        button.textContent = entry.text;
        button.dataset.id = entry.id;
        // A line the shop has sold out of is shown disabled with the fact on it: the product would refuse
        // the purchase by name, and a button that cannot work must not look like one that can.
        if (entry.available === false) button.disabled = true;
        if (entry.action !== undefined) {
          // The payload is read when the button is pressed rather than when it is drawn: a lesson goes to
          // whoever the picker shows at that moment, not to whoever it showed when the shelf was last redrawn.
          button.addEventListener('click', () => claim(entry.action!, entry.payload?.() ?? {}));
        } else {
          button.disabled = true;
        }

        list.append(button);
      }

      row.append(heading, list);
      return row;
    };

    stockRow.replaceChildren(
      view.stock.length === 0 && !view.operations.includes('buy')
        ? document.createElement('div')
        : offers(
            'For sale',
            view.stock.map((entry) => ({
              id: entry.lot,
              text:
                entry.count === 0
                  ? `${entry.name} — sold out`
                  : `${entry.name} ×${entry.count} — ${entry.price}${entry.sale ? ' (yours)' : ''}`,
              action: view.operations.includes('buy') ? ACTION_SERVICE_BUY : undefined,
              available: entry.count > 0,
              payload: () => ({ target: entry.lot, count: 1 }),
            })),
          ),
    );

    const saleOffers: {
      id: string;
      text: string;
      action?: string;
      payload?: () => Record<string, unknown>;
    }[] = [];
    for (const entry of view.sales) {
      const state = `${entry.damage > 0 ? `, damaged ${entry.damage}` : ''}${entry.identified ? '' : ', unidentified'}`;
      if (view.operations.includes('sell')) {
        saleOffers.push({
          id: `sell-${entry.item}`,
          text: `Sell ${entry.name}${state} — ${entry.price}`,
          action: ACTION_SERVICE_SELL,
          payload: () => ({ target: entry.item }),
        });
      }

      if (view.operations.includes('identify') && !entry.identified) {
        saleOffers.push({
          id: `identify-${entry.item}`,
          text: `Identify ${entry.name}`,
          action: ACTION_SERVICE_IDENTIFY,
          payload: () => ({ target: entry.item }),
        });
      }

      if (view.operations.includes('repair') && entry.damage > 0) {
        saleOffers.push({
          id: `repair-${entry.item}`,
          text: `Repair ${entry.name}`,
          action: ACTION_SERVICE_REPAIR,
          payload: () => ({ target: entry.item }),
        });
      }
    }

    saleRow.replaceChildren(saleOffers.length === 0 ? document.createElement('div') : offers('Your items', saleOffers));

    lessonRow.replaceChildren(
      view.lessons.length === 0
        ? document.createElement('div')
        : offers(
            'Taught here',
            view.lessons.map((entry) => ({
              // A skill can be taught at more than one rung, so the row's identity is the subject and the
              // rung together: two lessons of one skill are two rows, and the one a player presses is the
              // one the command names.
              id: `${entry.subject}@${entry.tier}`,
              // A lesson that grants a skill says the level it leaves it at; one that grants a rung of a
              // skill says the rung in its own name, which the product composed.
              text:
                entry.kind === 'skill' && entry.tier <= 1
                  ? `${entry.name} to level ${entry.amount} — ${entry.price}`
                  : `${entry.name} — ${entry.price}`,
              action: view.operations.includes('teach') ? ACTION_SERVICE_TEACH : undefined,
              payload: () => ({
                target: entry.subject,
                tier: entry.tier,
                member: Number(serviceMemberSelect.value === '' ? '0' : serviceMemberSelect.value),
              }),
            })),
          ),
    );
  };

  /**
   * Renders the stop controls and what the last one did. The buttons follow the mechanism rather than the
   * mode, because a stop is an instant: a held session still lets the party sleep, exactly as it still lets
   * it use what it faces. What a night cost is written out in full — the period, the larder, and the clock's
   * own before and after — because "the party rested" and "the party rested and it cost two portions" are
   * different facts, and the refusal keeps the sentence the product answered with.
   */
  const renderRest = (view: RestView): void => {
    // A session with no mechanism, a party that has not stopped, and a refused stop are three different
    // facts: a panel that hid the section for the first would leave a product without the mechanism looking
    // like a party that had simply not slept yet.
    panel.dataset.rest = !view.available ? 'none' : view.outcome === 'none' ? 'ready' : view.outcome;
    panel.dataset.restAction = view.kind;
    panel.dataset.restOutcome = view.outcome;
    panel.dataset.tired = view.tired ? 'yes' : 'no';
    rest.hidden = !view.available;
    restState.textContent = view.available
      ? view.tired
        ? `Tired${view.fatigueDue === '' ? '' : ` · next sleep due ${view.fatigueDue}`}${
            view.fatigueLanded > 0 ? ` · landed ${view.fatigueLanded}×` : ''
          }`
        : `Rested${view.fatigueDue === '' ? '' : ` · next sleep due ${view.fatigueDue}`}`
      : '';
    for (const button of restActions.querySelectorAll('button')) button.disabled = !view.available;
    restResult.hidden = view.message === '';
    restResult.dataset.outcome = view.outcome;
    restResult.dataset.code = view.code;
    if (view.message === '') {
      restResult.textContent = '';
    } else {
      // What a night cost and what it cleared are both appended when both are known: a rest that spent the
      // last of the larder and left the party weak afterwards is exactly the case a player needs to read in
      // full, and a screen that showed the cost or the clearing would hide half of it.
      const cost = view.charged > 0 ? ` Cost ${view.covered} of ${view.charged} ${view.unit}.` : '';
      const cleared = view.cleared === '' ? '' : ` Cleared: ${view.cleared}.`;
      restResult.textContent = `${view.message}${cost}${cleared}`;
    }
  };

  /**
   * Renders the fight and the last order's answer. Readiness is printed as the product published it: a member
   * that may act says so, and one that is recovering says how much game time it still owes. Nothing here
   * counts that time down — the product republishes it, and a screen that ran its own clock would show a
   * character ready before the fight agreed.
   */
  const renderCombat = (view: CombatView): void => {
    panel.dataset.combat = !view.available ? 'none' : view.engaged ? 'engaged' : 'quiet';
    panel.dataset.combatReady = String(view.ready);
    panel.dataset.combatOpposition = String(view.opposition);
    panel.dataset.combatOutcome = view.outcome;
    combat.hidden = !view.available;
    combatState.textContent = !view.available
      ? ''
      : view.engaged
        ? `Engaged with ${view.opposition} — ${view.ready} of ${view.members.length} ready`
        : `Nobody is hostile — ${view.ready} of ${view.members.length} ready`;
    // Every fact a fighter row shows is the product's own: what it is called, what it has left to lose,
    // what is acting on it, and whether it is out of the fight. Nothing here is derived from a bar or a
    // countdown the screen runs for itself, and a death is a row that says so rather than a missing row.
    const fighter = (actor: FighterView, at = ''): HTMLLIElement => {
      const row = document.createElement('li');
      row.className = 'crawler-fighter';
      row.dataset.ready = actor.ready ? 'yes' : 'no';
      row.dataset.fighter = actor.id;
      row.dataset.health = actor.hitPointsMax > 0 ? `${actor.hitPoints}/${actor.hitPointsMax}` : '';
      row.dataset.conditions = actor.conditions;
      row.dataset.down = actor.down ? 'yes' : 'no';
      // What the row says of the actor is the product's readiness word, and being out of the fight outranks
      // it: a laid-out character is not recovering toward an act, and a row that read "recovering 0.0s"
      // would be showing a ready light for somebody who cannot act.
      const word = actor.down
        ? 'down'
        : actor.ready
          ? 'ready'
          : `recovering ${actor.recoverySeconds.toFixed(1)}s`;
      const pools = actor.hitPointsMax > 0 ? ` — ${actor.hitPoints}/${actor.hitPointsMax} hp` : '';
      const conditions = actor.conditions === '' ? '' : ` — ${actor.conditions}`;
      // What a driven actor is doing is its own fact and is printed beside the state — unless it is the same
      // word, which is what a creature that has gone down reports twice.
      const doing = actor.activity === '' || actor.activity === word ? '' : ` — ${actor.activity}`;
      row.dataset.activity = actor.activity;
      row.textContent = `${actor.name} — ${word}${at}${pools}${conditions}${doing}`;
      return row;
    };

    combatMembers.replaceChildren(...view.members.map((member) => fighter(member)));
    combatEnemies.replaceChildren(
      ...view.enemies.map((enemy) => fighter(enemy, ` at ${enemy.distance.toFixed(0)}`)),
    );
    // The pacing is printed as the product published it, with the round it is in and what the actor whose
    // turn it is still owes: a player pressing the act key needs to know whether the world is waiting for
    // them, and nothing here works that out for itself.
    const paced = view.pacing === 'turnbased';
    panel.dataset.pacing = view.available ? view.pacing : 'none';
    panel.dataset.turnPhase = view.turn.phase;
    panel.dataset.turnRound = String(view.turn.round);
    panel.dataset.turnActor = view.turn.actorName;
    panel.dataset.turnPlayer = view.turn.playerTurn ? 'yes' : 'no';
    panel.dataset.turnLast = view.turn.last;
    combatTurn.textContent = !view.available
      ? ''
      : !paced
        ? 'Real time: actors act as their own recovery elapses.'
        : view.turn.phase === 'movement'
          ? `Move: round ${view.turn.round}, ${view.turn.movementSeconds.toFixed(1)}s of movement left — any turn control ends it.`
          : view.turn.phase === 'action'
            ? `Round ${view.turn.round} — ${view.turn.actorName}'s turn${
                view.turn.playerTurn ? ' (yours)' : ''
              }${view.turn.dueSeconds > 0 ? `, due in ${view.turn.dueSeconds.toFixed(1)}s` : ''}${
                view.turn.roundSeconds > 0 ? ` · ${view.turn.roundSeconds.toFixed(1)}s round` : ''
              }${view.turn.last === '' ? '' : ` · last turn ${view.turn.last}`}`
            : 'Turn-based: nothing is being fought, so no round is under way.';
    // The order is the fight's own reading of each actor's recovery, printed with the amount each still owes:
    // the row whose turn it is says so, and a row that has deferred its turn says that too.
    combatOrder.replaceChildren(
      ...view.turn.order.map((actor) => {
        const row = document.createElement('li');
        row.className = 'crawler-turn-actor';
        row.dataset.turnActor = actor.id;
        row.dataset.side = actor.side;
        row.dataset.ready = actor.ready ? 'yes' : 'no';
        row.dataset.current = actor.current ? 'yes' : 'no';
        row.dataset.waiting = actor.waiting ? 'yes' : 'no';
        row.dataset.remaining = actor.remainingSeconds.toFixed(3);
        const state = !actor.canAct
          ? ' — out of the fight'
          : actor.ready
            ? ' — ready'
            : ` — ${actor.remainingSeconds.toFixed(1)}s`;
        row.textContent = `${actor.name}${state}${actor.current ? ' — this turn' : ''}${
          actor.waiting ? ' — waiting' : ''
        }`;
        return row;
      }),
    );

    // Whether a round of a paced fight is actually under way is the product's own word for the phase: `none`
    // is what real time publishes, and what a paced fight with nothing to pace publishes too. Every control
    // below follows that one published fact rather than working a round out for itself.
    const roundHolds = view.turn.phase === 'action' || view.turn.phase === 'movement';

    // The act control is offered exactly when the product would take the order. Outside a round that is when
    // somebody may act — the panel's own readiness count, so a party that is entirely recovering or entirely
    // laid out keeps it disabled rather than sending an order the fight answers with a refusal. Inside one the
    // act is the party's turn, which is what the product says the session is waiting for, and in the movement
    // phase it is the act that ends the phase; on the party's turn it stays offered even when that actor's own
    // recovery is still owed, because there the refusal is the answer a player has to see.
    const actsInRound = view.turn.phase === 'movement' || view.turn.playerTurn;
    attackButton.disabled = !view.available || (roundHolds ? !actsInRound : view.ready === 0);
    // Switching the pacing is offered whenever there is a fight mechanism to pace; the two turn actions are
    // offered while a round is under way, because outside one there is no turn of the player's to pass and
    // the product's answer would be a refusal this panel has nowhere to show.
    paceButton.disabled = !view.available;
    paceButton.textContent = paced ? 'Real-time' : 'Turn-based';
    skipButton.disabled = !view.available || !paced || !roundHolds;
    waitButton.disabled = !view.available || !paced || !roundHolds;
    combatResult.hidden = view.message === '';
    combatResult.dataset.outcome = view.outcome;
    combatResult.dataset.code = view.code;
    // What the last attack came to, in the product's own numbers: whether it landed, what it rolled, what
    // the target's resistance took off it, what was left, and whether anything was left on the target.
    combatResult.dataset.resolved = view.resolved ? 'yes' : 'no';
    combatResult.dataset.hit = view.hit ? 'yes' : 'no';
    combatResult.dataset.chance = String(view.chance);
    combatResult.dataset.damage = String(view.damage);
    combatResult.dataset.damageRolled = String(view.damageRolled);
    combatResult.dataset.damageKind = view.damageKind;
    combatResult.dataset.resistance = view.resistance;
    combatResult.dataset.condition = view.condition;
    combatResult.dataset.targetDown = view.targetDown ? 'yes' : 'no';
    // Whose blow the last order was: the party's own or a creature's. The sentence is the product's own and
    // is shown unchanged; the fact of which side acted is what the attribute and the styling carry, so a
    // wound the party took does not read as the party's own doing.
    combatResult.dataset.byParty = view.byParty ? 'yes' : 'no';
    combatResult.textContent = view.message;
  };

  /**
   * Renders what each member has earned and what a level would cost. Every number is the product's own: the
   * experience against the curve it published, the points it published, and the fee the counter quoted. The
   * train control is built per member and only where the counter trains them — a cap of zero is the
   * product's own way of saying that nobody here trains — so a button this panel offers is one the product
   * would take.
   */
  const renderProgression = (view: ProgressionView): void => {
    panel.dataset.progression = view.available ? 'present' : 'none';
    panel.dataset.progressionOutcome = view.outcome;
    progression.hidden = !view.available;
    progressionState.textContent = view.available
      ? `${view.members.length} character${view.members.length === 1 ? '' : 's'}`
      : '';
    progressionResult.hidden = view.message === '';
    progressionResult.dataset.outcome = view.outcome;
    progressionResult.dataset.code = view.code;
    progressionResult.textContent = view.message;

    const signature = JSON.stringify(view);
    if (signature === renderedProgression) return;
    renderedProgression = signature;

    progressionMembers.replaceChildren(
      ...view.members.map((member) => {
        const row = document.createElement('div');
        row.className = 'crawler-progression-member';
        row.dataset.member = member.member;
        row.dataset.level = String(member.level);
        const label = document.createElement('span');
        label.className = 'crawler-row-label';
        // The curve is the product's own figure, so "how far along" is printed rather than worked out: the
        // experience banked, what the next level takes, the level itself, and the points still to spend.
        label.textContent = `${member.name} — level ${member.level} — ${member.experience}/${member.nextLevel} xp — ${member.skillPoints} skill point${member.skillPoints === 1 ? '' : 's'}`;
        row.append(label);
        if (member.cap > 0) {
          const train = document.createElement('button');
          train.type = 'button';
          train.className = 'crawler-train';
          train.textContent = `Train to level ${member.level + 1} for ${member.fee} gold (up to ${member.cap})`;
          train.addEventListener('click', () => claim(ACTION_SERVICE_TRAIN, { member: member.index }));
          row.append(train);
        }

        return row;
      }),
    );
  };

  /**
   * Renders every member's spellbook and the controls that cast from it.
   *
   * Each row prints what the product published — the spell, its school, the rung it asks for, and what one
   * casting costs this caster — and offers the target the spell's own aim allows, chosen from the actors the
   * product listed with the side each is on. A spell that names nobody is cast with no target; a spell aimed
   * at an opponent offers only the opposition's rows, so the panel narrows nothing the product did not
   * already state, and the product judges the aim it is handed all the same.
   */
  const renderMagic = (view: MagicView): void => {
    panel.dataset.magic = view.available ? 'present' : 'none';
    panel.dataset.magicOutcome = view.outcome;
    magic.hidden = !view.available;
    magicState.textContent = view.available
      ? `${view.members.length} character${view.members.length === 1 ? '' : 's'} · ${view.targets.length} target${view.targets.length === 1 ? '' : 's'} in reach`
      : '';
    magicResult.hidden = view.message === '';
    magicResult.dataset.outcome = view.outcome;
    magicResult.dataset.code = view.code;
    magicResult.textContent = view.message;

    // What the casting changed, as the product's own readings of the state it changed: every fact is a name
    // and the value it reads as, so a panel that showed only the sentence would leave a player guessing
    // whether the cure restored anything.
    magicFacts.replaceChildren(
      ...view.facts.map((fact) => {
        const item = document.createElement('li');
        item.className = 'crawler-magic-fact';
        item.dataset.fact = fact.name;
        item.textContent = `${fact.name}: ${fact.value}`;
        return item;
      }),
    );
    magicFacts.hidden = view.facts.length === 0;

    // What the party sees by, and what spells have left running with the moment each one lapses. The panel
    // shows the effect identity the product published and never interprets it.
    magicRunning.dataset.sight = view.sight;
    magicRunning.textContent = [
      view.sight === '' ? '' : `sight: ${view.sight}`,
      view.running.length === 0
        ? ''
        : `running: ${view.running
            .map((effect) => `${effect.effect} (${effect.magnitude})${effect.endsAt === '' ? '' : ` until ${effect.endsAt}`}`)
            .join(', ')}`,
    ]
      .filter((part) => part !== '')
      .join(' · ');
    magicRunning.hidden = magicRunning.textContent === '';

    const signature = JSON.stringify(view);
    if (signature === renderedMagic) return;
    renderedMagic = signature;

    /** The targets a spell's aim offers, as the product stated them. */
    const candidates = (targeting: string): readonly SpellTargetView[] => {
      if (targeting === 'foe') return view.targets.filter((target) => target.side === 'opposition');
      if (targeting === 'ally') return view.targets.filter((target) => target.side === 'party');
      return [];
    };

    magicMembers.replaceChildren(
      ...view.members.map((member) => {
        const block = document.createElement('div');
        block.className = 'crawler-magic-member';
        block.dataset.member = member.member;
        const label = document.createElement('span');
        label.className = 'crawler-row-label';
        label.textContent = `${member.name} — ${member.class} · ${member.spellPoints}/${member.spellPointsMax} spell points · quick ${member.quickSpellName === '' ? 'none' : member.quickSpellName}`;
        block.append(label);

        for (const row of member.spells) {
          const line = document.createElement('div');
          line.className = 'crawler-spell';
          line.dataset.spell = row.spell;
          line.dataset.school = row.school;
          line.dataset.targeting = row.targeting;
          const text = document.createElement('span');
          text.textContent = `${row.name} · ${row.school} · ${row.tier} · ${row.cost} point${row.cost === 1 ? '' : 's'} · ${row.targeting} · ${row.effect}`;
          line.append(text);

          // A spell whose aim names an actor offers that side's rows; a spell whose aim names no actor offers
          // what the product said it may be pointed at — the places a portal reaches, the beacon the party
          // set — and a spell neither names is cast with no target at all. The panel chooses nothing: it
          // draws the rows and echoes back the identity of the one a player picked.
          const options = candidates(row.targeting);
          let picker: HTMLSelectElement | null = null;
          if (options.length > 0) {
            picker = document.createElement('select');
            picker.className = 'crawler-target';
            for (const option of options) {
              const choice = document.createElement('option');
              choice.value = option.target;
              choice.textContent = option.name;
              picker.append(choice);
            }

            line.append(picker);
          } else if (row.aims.length > 0) {
            picker = document.createElement('select');
            picker.className = 'crawler-target';
            for (const aim of row.aims) {
              const choice = document.createElement('option');
              choice.value = aim.aim;
              choice.textContent = `${aim.name} (${aim.kind})`;
              picker.append(choice);
            }

            line.append(picker);
          }

          const cast = document.createElement('button');
          cast.type = 'button';
          cast.className = 'crawler-cast';
          cast.textContent = `Cast for ${row.cost}`;
          cast.disabled = options.length === 0 && row.aims.length === 0 && (row.targeting === 'foe' || row.targeting === 'ally');
          cast.addEventListener('click', () =>
            claim(ACTION_CAST, {
              member: member.index,
              spell: row.spell,
              target: picker === null ? '' : picker.value,
            }),
          );
          line.append(cast);

          const quick = document.createElement('button');
          quick.type = 'button';
          quick.className = 'crawler-quick';
          quick.textContent = member.quickSpell === row.spell ? 'Clear quick spell' : 'Make quick spell';
          quick.addEventListener('click', () =>
            claim(ACTION_QUICK_SPELL, {
              member: member.index,
              spell: member.quickSpell === row.spell ? '' : row.spell,
            }),
          );
          line.append(quick);
          block.append(line);
        }

        return block;
      }),
    );
  };

  /**
   * Renders every member's skills and the control that spends a point on one.
   *
   * Each row prints what the product published — the level, the rung's own word, the ceiling the class and
   * rank impose, and what the next point would cost or the sentence that refuses it. A row the product would
   * refuse carries the refusal and no control, so a button this panel offers is one the product would take;
   * a row it would accept carries the price the product quoted and asks for exactly that raise.
   */
  const renderSkills = (view: SkillsView): void => {
    panel.dataset.skills = view.available ? 'present' : 'none';
    panel.dataset.skillsOutcome = view.outcome;
    skills.hidden = !view.available;
    skillsState.textContent = view.available
      ? `${view.members.length} character${view.members.length === 1 ? '' : 's'}`
      : '';
    skillsResult.hidden = view.message === '';
    skillsResult.dataset.outcome = view.outcome;
    skillsResult.dataset.code = view.code;
    skillsResult.textContent = view.message;

    const signature = JSON.stringify(view);
    if (signature === renderedSkills) return;
    renderedSkills = signature;

    skillsMembers.replaceChildren(
      ...view.members.map((member) => {
        const block = document.createElement('div');
        block.className = 'crawler-skills-member';
        block.dataset.member = member.member;
        const label = document.createElement('span');
        label.className = 'crawler-row-label';
        label.textContent = `${member.name} — ${member.class} of rank ${member.rank}`;
        block.append(label);

        for (const row of member.skills) {
          const line = document.createElement('div');
          line.className = 'crawler-skill';
          line.dataset.skill = row.skill;
          line.dataset.block = row.block;
          line.dataset.tier = row.tier;
          line.dataset.refusal = row.refusal;
          const text = document.createElement('span');
          // What the row reads: the skill, the rung it stands at, how far it has come against the ceiling
          // the product published, and either the price of the next point or why there is none.
          text.textContent =
            row.refusal === ''
              ? `${row.skill} · ${row.block} · ${row.tier} · ${row.level}/${row.ceilingLevel} (${row.ceilingTier})`
              : `${row.skill} · ${row.block} · ${row.tier} · ${row.level}/${row.ceilingLevel} (${row.ceilingTier}) · ${row.refusal}`;
          if (row.refusal !== '') text.className = 'crawler-skill-refusal';
          line.append(text);

          if (row.refusal === '') {
            const raise = document.createElement('button');
            raise.type = 'button';
            raise.className = 'crawler-raise';
            raise.textContent = `Raise to ${row.reached} for ${row.cost} point${row.cost === 1 ? '' : 's'}`;
            raise.addEventListener('click', () =>
              claim(ACTION_RAISE, { member: member.index, skill: row.skill }),
            );
            line.append(raise);
          }

          block.append(line);
        }

        return block;
      }),
    );
  };

  const render = (snapshot: SnapshotView): void => {
    current = snapshot.session.mode;
    title.textContent = 'Rusty Crawler';
    ruleset.textContent = snapshot.composition.title;
    bundle.textContent =
      snapshot.composition.bundle === ''
        ? 'No game bundle selected'
        : `${snapshot.composition.bundle} · ${snapshot.composition.contentPacks} pack${snapshot.composition.contentPacks === 1 ? '' : 's'}`;
    panel.dataset.mode = current;
    panel.dataset.ruleset = snapshot.composition.ruleset;
    panel.dataset.bundle = snapshot.composition.bundle;
    rows.mode.textContent = current;
    rows.simulation.textContent = `${snapshot.session.simulationSeconds.toFixed(1)} s`;
    rows.steps.textContent = String(snapshot.session.admittedSteps);
    rows.updates.textContent = String(snapshot.session.updates);
    rows.content.textContent = String(snapshot.composition.contentPacks);
    // The clock's own facts, printed as they arrived: the date and the time are what the calendar and the
    // clock published, and the panel derives none of them. `present` is what tells a session whose ruleset
    // composed no clock from one standing on the first day of its calendar, and the two must not look alike.
    const { clock, party, creation: creating, save } = snapshot;
    panel.dataset.clock = clock.present ? 'present' : 'none';
    panel.dataset.party = party.present ? 'present' : 'none';
    panel.dataset.light = clock.present ? clock.daylight : 'unknown';
    rows.date.textContent = clock.present ? clock.date : '—';
    rows.time.textContent = clock.present ? `${clock.time} · ${clock.daylight}` : '—';
    rows.days.textContent = clock.present ? String(clock.elapsedDays) : '—';
    // The party's accounts and standing are read from the party the product holds, never counted here.
    rows.party.textContent = party.present ? String(party.members) : '—';
    // What the party carries and what it has: everything a search put in the shared pack is this number
    // moving, so what a corpse held is visible on the panel rather than only in the sentence about it.
    rows.pack.textContent = party.present ? String(party.pack) : '—';
    rows.coins.textContent = party.present ? String(party.coins) : '—';
    rows.food.textContent = party.present ? `${party.provisions} ${party.unit}` : '—';
    rows.standing.textContent = party.present ? `${party.reputation} / ${party.fame}` : '—';
    rows.condition.textContent = party.present && party.conditions !== '' ? party.conditions : '—';
    // What the party has left to lose and to cast with: a night's sleep restores the pools, and a panel that
    // showed only food and conditions would leave a rested party and a wounded one looking the same.
    rows.vitality.textContent = party.present ? `${party.hitPoints} / ${party.hitPointsMax}` : '—';
    rows.magic.textContent = party.present ? `${party.spellPoints} / ${party.spellPointsMax}` : '—';
    // The save's own facts: whether this session can save at all, whether it came from the slot, and what
    // happened the last time the player asked. Nothing is derived here — the outcome word, the moment, and
    // the reason are the product's, and the panel prints them unchanged.
    panel.dataset.save = save.state;
    rows.save.textContent =
      !save.available
        ? 'unavailable'
        : save.state === 'saved'
          ? `${save.at} · ${save.slot}`
          : save.state === 'failed'
            ? 'failed'
            : '—';
    rows.start.textContent = save.resumed ? 'resumed' : 'fresh';
    saveResult.hidden = save.message === '';
    saveResult.dataset.state = save.state;
    saveResult.dataset.code = save.code;
    saveResult.textContent = save.message;
    // The interaction's own facts: what the party faces, what it requires, and what the last use did. Every
    // word here is the product's — the reason the reticle holds nothing, the requirement a lock states, and
    // the sentence a refusal answered with — and the panel spells none of them itself.
    const { interaction } = snapshot;
    panel.dataset.interaction = interaction.available ? interaction.reason : 'none';
    panel.dataset.bodies = String(interaction.bodies);
    panel.dataset.use = interaction.outcome;
    rows.facing.textContent =
      !interaction.available
        ? '—'
        : interaction.label === ''
          ? interaction.reason
          : `${interaction.label} · ${interaction.verb}${interaction.state === '' ? '' : ` · ${interaction.state}`} · ${interaction.distance.toFixed(0)}`;
    rows.bodies.textContent = interaction.available ? String(interaction.bodies) : '—';
    rows.requires.textContent = interaction.requires.length === 0 ? '—' : interaction.requires.join(', ');
    rows.use.textContent = interaction.outcome === 'none' ? '—' : interaction.outcome;
    useResult.hidden = interaction.message === '';
    useResult.dataset.outcome = interaction.outcome;
    useResult.dataset.code = interaction.code;
    useResult.textContent = interaction.message;
    useResidue.hidden = interaction.residue === '';
    useResidue.textContent = interaction.residue;
    // Creation's own facts: which member is being made, where it stands, and what the pool still holds.
    rows.member.textContent = creating.active || creating.accepted ? `${creating.member + 1} / ${creating.members}` : '—';
    rows.creationStep.textContent = creating.step === '' ? '—' : creating.step;
    rows.pool.textContent = creating.active ? String(creating.pool) : '—';
    renderCreation(creating, save.resumed);
    renderConversation(snapshot.conversation);
    renderService(snapshot.service);
    renderRest(snapshot.rest);
    renderCombat(snapshot.combat);
    renderProgression(snapshot.progression);
    renderSkills(snapshot.skills);
    renderMagic(snapshot.magic);
    const world = snapshot.world;
    place.textContent =
      world.places === 0
        ? 'No world loaded'
        : `${world.name}${world.kind === '' ? '' : ` · ${world.kind}`}`;
    panel.dataset.place = world.place;
    rows.place.textContent = world.place === '' ? '—' : world.place;
    // The place's own hours are a clock read, recomputed by the product: a shop that shut at its closing
    // hour reads shut here beside the hour it shut by, and a place that keeps none says nothing.
    rows.hours.textContent =
      world.hours === ''
        ? '—'
        : `${world.hours} · ${world.open ? 'open' : 'closed'}${world.nextChange === '' ? '' : ` · next ${world.nextChange}`}`;
    rows.pose.textContent =
      world.places === 0
        ? '—'
        : `${world.x.toFixed(0)}, ${world.y.toFixed(0)}, ${world.z.toFixed(0)} @ ${world.yaw.toFixed(0)}`;
    rows.explored.textContent = `${world.visited} / ${world.places}`;
    const movement = snapshot.movement;
    // A session with no movement facts shows that it does not know what the last step did. Showing a
    // clear path there would be the very mistake this panel exists to prevent: a refused step and an
    // input that never arrived would look the same again.
    panel.dataset.motion = movement.motion;
    panel.dataset.blocked = movement.blocked;
    rows.motion.textContent = movement.motion === 'none' ? '—' : movement.motion;
    rows.blocked.textContent = movement.blocked === 'none' ? '—' : movement.blocked;
    rows.step.textContent = movement.stepRise > 0 ? `+${movement.stepRise.toFixed(1)}` : '—';
    rows.fall.textContent =
      movement.fallDistance > 0
        ? `${movement.fallDistance.toFixed(0)} · ${movement.fallDamage.toFixed(0)} damage`
        : '—';
    if (current === 'running' || current === 'turnbased') {
      action.disabled = false;
      action.textContent = 'Pause session';
    } else if (current === 'paused') {
      action.disabled = false;
      action.textContent = 'Resume session';
    } else if (current === 'creating') {
      action.disabled = true;
      action.textContent = 'Creating a party…';
    } else {
      action.disabled = true;
      action.textContent = current === 'stopped' ? 'Session stopped' : 'Starting…';
    }

    // The save control follows the session rather than the screen: there is nothing to save while a party
    // is being made, and a session with no store says so on its Save row instead of offering a button that
    // cannot work.
    saveButton.disabled = !save.available || (current !== 'running' && current !== 'paused');
    // The use control follows what is faced rather than the mode: a use is an instant, so a held session can
    // still use what it is looking at, and a session with nothing in front of the party offers the button
    // disabled with the reason on the Facing row.
    useButton.disabled =
      !interaction.available || interaction.label === '' || current === 'creating' || current === 'stopped';
    const pauseHint = 'Pause or resume with the button, or with the P key.';
    const saveHint = save.available ? ' Save with the button, or with the F key.' : '';
    const useHint = interaction.available ? ' Use with the button, or with the G key.' : '';
    const serviceHint = snapshot.service.open ? ' Leave the counter with the button, or with the X key.' : '';
    const restHint = snapshot.rest.available
      ? ' Rest with the button or the R key; camp with C; wait with T, H, or M.'
      : '';
    const magicHint = snapshot.magic.available ? ' Cast from the spellbook rows.' : '';
    const combatHint = snapshot.combat.available
      ? ` Attack with the button or the B key.${
          snapshot.combat.pacing === 'turnbased'
            ? ' Switch the pacing with the button or the Enter key; skip a turn with K; wait with Y.'
            : ' Switch to turn-based pacing with the button or the Enter key.'
        }`
      : '';
    hint.textContent =
      current === 'creating'
        ? 'Choose a portrait, a class, a name, attributes, and skills. Enter confirms the step you are on; Space accepts a finished party.'
        : `${pauseHint}${saveHint}${useHint}${serviceHint}${restHint}${combatHint}${magicHint}`;
  };

  const unsubscribe = context.projection?.subscribe((projection) => {
    if (projection?.contract !== UI_CONTRACT) return;
    const snapshot = readSnapshot(projection.value);
    if (snapshot !== null) render(snapshot);
  });

  return {
    dispose(): void {
      unsubscribe?.();
      panel.remove();
      style.remove();
    },
  };
}
