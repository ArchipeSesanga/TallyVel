// Runs the whole Vitest suite N times in a row (default 10) and stops at the first failure.
// A test that fails even once in ten runs is a flaky test, and a flaky test is a bug.
// Usage: npm run test:repeat            (10 runs)
//        node scripts/repeat-vitest.mjs 3
import { spawnSync } from 'node:child_process'

const runs = Number(process.argv[2] ?? 10)
for (let i = 1; i <= runs; i++) {
  const result = spawnSync('npx', ['vitest', 'run', '--reporter=dot'], { encoding: 'utf8', env: process.env })
  const summary = result.stdout.split('\n').filter((l) => /Test Files|Tests /.test(l)).map((l) => l.trim()).join(' | ')
  console.log(`run ${String(i).padStart(2)}/${runs}: ${result.status === 0 ? 'PASS' : 'FAIL'}  ${summary}`)
  if (result.status !== 0) {
    console.log(result.stdout, result.stderr)
    process.exit(1)
  }
}
console.log(`all ${runs} runs passed`)
