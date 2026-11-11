import { test } from 'node:test'
import assert from 'node:assert/strict'
import { vilniusInput, vilniusToIso } from '../../src/SteamIRL.Web/src/sessions/time.ts'

test('planned times are converted using Vilnius rather than browser timezone', () => {
  assert.equal(vilniusToIso('2026-11-04T18:00'), '2026-11-04T16:00:00.000Z')
  assert.equal(vilniusToIso('2027-07-01T18:00'), '2027-07-01T15:00:00.000Z')
  assert.equal(vilniusInput(Date.parse('2026-11-04T16:00:00Z')), '2026-11-04T18:00')
})
test('nonexistent and ambiguous DST times are rejected instead of silently shifted', () => {
  assert.throws(() => vilniusToIso('2027-03-28T03:30'), /Такого времени нет/)
  assert.throws(() => vilniusToIso('2027-10-31T03:30'), /повторяется/)
})
test('invalid calendar values are rejected', () => {
  assert.throws(() => vilniusToIso('2027-02-30T18:00'))
  assert.throws(() => vilniusToIso('not-a-time'))
})
