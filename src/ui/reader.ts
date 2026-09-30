/**
 * The one way a block of the projection is read: every field by name and type, and every field that is missing
 * or of another type reported by its path.
 *
 * The product publishes every block in every mode, with every field present — the C# projection is the contract,
 * and the companion suite mounts fixtures the product itself wrote. A field this panel asks for and does not find
 * is therefore a broken contract rather than a quiet session, and it is reported where the suite and a person can
 * read it instead of being rounded to a zero or an empty section that looks like a real reading. What the panel
 * then shows is the block's no-mechanism reading, never a half-read block: a number it could not read is not one
 * it prints.
 */

/** Where the problems met while reading one projection are collected, in the order they were met. */
export type Problems = string[];

/** One object of the projection, read field by field. */
export interface Fields {
  /** Where the object sits in the projection, for the problems it reports. */
  readonly path: string;
  /** A string field, or the fallback when it is missing or of another type. */
  text(key: string, fallback?: string): string;
  /** A finite number field, or the fallback when it is missing or of another type. */
  number(key: string, fallback?: number): number;
  /** A boolean field, or false when it is missing or of another type. */
  flag(key: string): boolean;
  /** An array of strings. */
  words(key: string): string[];
  /** An array of finite numbers. */
  numbers(key: string): number[];
  /** An array of objects, each read by `read`. */
  list<T>(key: string, read: (entry: Fields) => T): T[];
  /** A nested object, which reads as absent (and reports it) when it is missing. */
  object(key: string): Fields;
  /** A nested object the product may publish as null, which is then read as null without a problem. */
  nullable<T>(key: string, read: (entry: Fields) => T): T | null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function kind(value: unknown): string {
  if (value === undefined) return 'nothing';
  if (value === null) return 'null';
  if (Array.isArray(value)) return 'an array';
  return typeof value === 'object' ? 'an object' : `a ${typeof value}`;
}

/**
 * An object the product did not publish. Reading it gives every field's fallback and reports nothing further:
 * the one problem is that the object itself was missing, and that was reported where it was met.
 */
const ABSENT: Fields = {
  path: '',
  text: (_key, fallback = '') => fallback,
  number: (_key, fallback = 0) => fallback,
  flag: () => false,
  words: () => [],
  numbers: () => [],
  list: () => [],
  object: () => ABSENT,
  nullable: () => null,
};

/** The absent object: what a block's no-mechanism reading is read from. */
export const absent: Fields = ABSENT;

/** Reads `value` as an object at `path`, reporting into `problems`; a value that is not one reads as absent. */
export function fields(value: unknown, path: string, problems: Problems): Fields {
  if (!isRecord(value)) {
    problems.push(`${path}: expected an object, found ${kind(value)}`);
    return ABSENT;
  }

  const at = (key: string): string => (path === '' ? key : `${path}.${key}`);
  const wrong = (key: string, expected: string, found: unknown): void => {
    problems.push(`${at(key)}: expected ${expected}, found ${kind(found)}`);
  };

  return {
    path,
    text(key, fallback = '') {
      const found = value[key];
      if (typeof found === 'string') return found;
      wrong(key, 'a string', found);
      return fallback;
    },
    number(key, fallback = 0) {
      const found = value[key];
      if (typeof found === 'number' && Number.isFinite(found)) return found;
      wrong(key, 'a finite number', found);
      return fallback;
    },
    flag(key) {
      const found = value[key];
      if (typeof found === 'boolean') return found;
      wrong(key, 'a boolean', found);
      return false;
    },
    words(key) {
      const found = value[key];
      if (!Array.isArray(found)) {
        wrong(key, 'an array', found);
        return [];
      }

      return found.flatMap((entry, index) => {
        if (typeof entry === 'string') return [entry];
        problems.push(`${at(key)}[${index}]: expected a string, found ${kind(entry)}`);
        return [];
      });
    },
    numbers(key) {
      const found = value[key];
      if (!Array.isArray(found)) {
        wrong(key, 'an array', found);
        return [];
      }

      return found.flatMap((entry, index) => {
        if (typeof entry === 'number' && Number.isFinite(entry)) return [entry];
        problems.push(`${at(key)}[${index}]: expected a finite number, found ${kind(entry)}`);
        return [];
      });
    },
    list(key, read) {
      const found = value[key];
      if (!Array.isArray(found)) {
        wrong(key, 'an array', found);
        return [];
      }

      return found.map((entry, index) => read(fields(entry, `${at(key)}[${index}]`, problems)));
    },
    object(key) {
      return fields(value[key], at(key), problems);
    },
    nullable(key, read) {
      const found = value[key];
      return found === null ? null : read(fields(found, at(key), problems));
    },
  };
}

/**
 * Reads one block of the projection: the block as published when every field of it could be read, and its
 * no-mechanism reading when any could not. The problems are kept either way, so a block the panel could not read
 * is both shown as unknown and named as broken.
 */
export function block<T>(root: Fields, key: string, read: (entry: Fields) => T, problems: Problems): T {
  const before = problems.length;
  const view = read(root.object(key));
  return problems.length === before ? view : read(ABSENT);
}
