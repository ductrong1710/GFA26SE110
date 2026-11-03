import fs from 'node:fs'
import path from 'node:path'

const roots = ['src', 'tests', 'scripts']
const files = []
function walk(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const file = path.join(directory, entry.name)
    if (entry.isDirectory()) walk(file)
    else if (/\.(?:[cm]?js|jsx|tsx?|css|ps1)$/.test(file)) files.push(file)
  }
}
roots.filter(root => fs.existsSync(root)).forEach(walk)
const counts = files.map(file => ({ file, lines: fs.readFileSync(file, 'utf8').trimEnd().split(/\r?\n/).length }))
const failures = counts.filter(row => row.lines > 300)
if (failures.length) {
  console.error('Mỗi file code phải không quá 300 dòng:', failures)
  process.exitCode = 1
} else {
  const largest = counts.reduce((a, b) => a.lines > b.lines ? a : b)
  console.log(`Đã kiểm tra ${counts.length} file code. Tất cả <= 300 dòng. Lớn nhất: ${largest.file} (${largest.lines} dòng).`)
}
