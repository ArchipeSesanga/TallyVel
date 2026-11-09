// The two things the join address carries, as strict nuqs parsers. Read on the server with the loader.
//
// Why the address and not React state or a cookie: the address is the only place that survives a reload,
// works with the Back button, and is what an invite link already is (/join?code=…). A cookie would break the
// invite link; state would be lost on reload.
import { createLoader, createParser, parseAsStringLiteral } from 'nuqs/server'
import { CODE_PATTERN, JOIN_STEPS } from './joinFlow'

/** The invite code: letters, digits, "_" and "-", 1 to 64 characters. Anything else counts as "no code". */
const parseAsInviteCode = createParser({
  parse: (value) => (CODE_PATTERN.test(value.trim()) ? value.trim() : null),
  serialize: (value) => value,
})

export const joinParsers = {
  code: parseAsInviteCode, // null = none (or not a possible code)
  step: parseAsStringLiteral(JOIN_STEPS), // null = none (or not one of our three)
}

export const loadJoinParams = createLoader(joinParsers)
