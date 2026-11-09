import AxeBuilder from '@axe-core/playwright'
import { expect, type Page, type TestInfo } from '@playwright/test'

// The one place that decides what "accessible enough for the automated check" means.
//
// The tags are WCAG 2.2 AA: every rule axe has for A and AA of 2.0, 2.1 and 2.2. NO rule is switched off anywhere in
// the suite. (If one ever has to be, it is done here, in this one place, with the reason written next to it.)
export const WCAG_22_AA = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']

/**
 * Runs axe on the page as it is right now and fails on any SERIOUS or CRITICAL violation. Less serious findings
 * (moderate, minor) are not hidden: they are attached to the test report so they can be read and fixed.
 */
export async function expectNoSeriousViolations(page: Page, testInfo: TestInfo, scope?: string): Promise<void> {
  // Every skeleton in this app is marked aria-busy. Checking while one is still on screen would check the placeholder, not the page.
  await expect(page.locator('[aria-busy="true"]')).toHaveCount(0)

  let builder = new AxeBuilder({ page }).withTags(WCAG_22_AA)
  if (scope !== undefined) builder = builder.include(scope)
  const { violations } = await builder.analyze()

  const worth = violations.filter((v) => v.impact === 'serious' || v.impact === 'critical')
  const rest = violations.filter((v) => !worth.includes(v))
  if (rest.length > 0) {
    await testInfo.attach('axe-less-serious-findings', { body: JSON.stringify(rest, null, 2), contentType: 'application/json' })
  }

  // The message names every failing rule and the elements, so a red run says what to fix without opening the report.
  const summary = worth.map((v) => `${v.id} (${v.impact}): ${v.help}\n${v.nodes.map((n) => `    ${n.target.join(' ')}\n      ${n.failureSummary?.replace(/\n/g, '\n      ')}`).join('\n')}`)
  if (worth.length > 0) throw new Error(`axe found ${worth.length} serious or critical violation(s):\n\n${summary.join('\n\n')}`)
}
