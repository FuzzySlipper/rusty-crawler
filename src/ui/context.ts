/**
 * What the Engine hands a product's DOM companion when it mounts it, declared here as the Engine delivers it.
 *
 * The Engine does not ship these declarations with the pinned package yet (Engine #8870 will), so they are
 * written from the Engine's own application host at the pinned revision
 * (`render/packages/application-host/src/application-host.ts`, `ui-projection.ts`, and `input-ingress.ts`) and
 * trimmed to nothing: every member the context carries is declared, including the ones this companion does not
 * call, so a companion that reached for something the Engine does not deliver fails to compile instead of
 * failing in a browser. The companion suite's fake context is built to the same declarations.
 *
 * What the Engine guarantees, and this companion relies on:
 * - the context and every port on it are frozen;
 * - `projection.subscribe` delivers the current envelope synchronously before it returns, and that envelope may
 *   be `null` — there is none before the product first publishes, and one is published as `null` again when the
 *   runtime is rebound or the host is disposed;
 * - every envelope and every value inside it is deeply frozen, so nothing here may write into what it was given;
 * - `intents.claim` is synchronous, returns nothing, and throws on a malformed intent or payload.
 */

/** A runtime identity, every part of it an unsigned 64-bit number written in decimal. */
export interface RuntimeIdentity {
  readonly instanceId: string;
  readonly generation: string;
  readonly controlRevision: string;
}

/** One published projection, as the Engine delivers it. */
export interface ProjectionEnvelope {
  readonly artifact: 'rusty.product.ui-projection';
  readonly runtime: RuntimeIdentity;
  /** Strictly increasing within one runtime epoch, as decimal text. */
  readonly sequence: string;
  /** The stream the product declared. */
  readonly stream: string;
  /** The contract the value is written in; any other contract is not this companion's to read. */
  readonly contract: string;
  /** Plain JSON, deeply frozen. */
  readonly value: unknown;
}

/** The product's projection, as the companion reads it. */
export interface ProjectionView {
  /** The envelope published last, or null when there is none. */
  current(): ProjectionEnvelope | null;
  /** Delivers the current envelope at once, then every later one; returns the unsubscribe. */
  subscribe(listener: (projection: ProjectionEnvelope | null) => void): () => void;
}

/** Who has the keyboard: the game view, the interface, or a modal on top of both. */
export type InteractionMode = 'gameplay' | 'interface' | 'modal';

/**
 * The Engine's interface port. Keyboard input reaches the game only while the game view holds focus, so a control
 * this panel claims an action through hands focus back to it afterwards.
 */
export interface UiPort {
  active(): boolean;
  allowsGameplayInput(event: Event): boolean;
  focusGameplay(): void;
  interactionMode(): InteractionMode;
  setInteractionMode(mode: InteractionMode): void;
}

/** A plain JSON value, which is all a product payload may carry. */
export type PayloadJson = null | boolean | number | string | readonly PayloadJson[] | { readonly [key: string]: PayloadJson };

/** What claiming an intent sends. */
export type IntentValue =
  | { readonly kind: 'digital'; readonly active: boolean }
  | { readonly kind: 'axis'; readonly value: number }
  | { readonly kind: 'product-payload'; readonly contract: string; readonly data: PayloadJson };

/** The Engine's intent port: a claim is queued into the ordered input lane the product's update reads. */
export interface IntentsPort {
  claim(intent: string, value: IntentValue): void;
}

/** One controller reading the Engine delivers while the interface holds input. */
export interface InterfaceInputObservation {
  readonly context: 'interface';
  readonly fact: { readonly kind: 'controller-button' | 'controller-axis' | 'controller-button-value' };
}

/** The Engine's interface input port, which delivers only while the interaction mode is `interface`. */
export interface InputPort {
  subscribe(observer: (input: InterfaceInputObservation) => void): () => void;
}

/** Everything the Engine hands the companion when it mounts it. */
export interface ProductUiContext {
  readonly ui: UiPort;
  /** Present when the product declares a projection stream, which this one does. */
  readonly projection?: ProjectionView;
  /** Present when the Engine runs the product's input, which the runtime shell always does. */
  readonly intents?: IntentsPort;
  readonly input?: InputPort;
}
