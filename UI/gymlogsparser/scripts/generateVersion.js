// scripts/generateVersion.js
import { writeFileSync } from 'node:fs'
import { execSync } from 'node:child_process'

function git(cmd, fallback = 'unknown') {
  try {
    return execSync(cmd).toString().trim()
  } catch {
    return fallback
  }
}

function getCommitAgeSeconds() {
  try {
    const commitTime = Number(execSync('git log -1 --pretty=%ct').toString().trim())

    const now = Math.floor(Date.now() / 1000)
    return now - commitTime
  } catch {
    return null
  }
}

const version = {
  build: git('git rev-parse --short HEAD'),
  commitTitle: git('git log -1 --pretty=%s'),
  time: new Date().toISOString(),
  buildTime: getCommitAgeSeconds(),
}

// Cloudflare Pages output dirr
// usually: dist, build, out, public
const OUT_DIR = process.env.PAGES_OUTPUT_DIR || 'dist'

writeFileSync(`${OUT_DIR}/version.json`, JSON.stringify(version, null, 2))

console.log('✅ version.json generated:', version)
