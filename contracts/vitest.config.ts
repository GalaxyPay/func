import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    // E2E specs run against LocalNet, so give transactions room to confirm.
    include: ['smart_contracts/**/*.e2e.spec.ts'],
    testTimeout: 60_000,
    hookTimeout: 60_000,
    // LocalNet is shared global state; don't run specs against it in parallel.
    fileParallelism: false,
  },
})
