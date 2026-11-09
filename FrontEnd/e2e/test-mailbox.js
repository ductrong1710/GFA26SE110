import { createServer } from 'node:net'
import { createInterface } from 'node:readline'
import { mkdtempSync, writeFileSync, rmSync, rmdirSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { once } from 'node:events'

async function receive(socket, mailbox) {
  socket.setTimeout(15000, () => socket.destroy())
  socket.write('220 localhost test mailbox\r\n')
  let recipient = false
  let message
  const lines = createInterface({ input: socket, crlfDelay: Infinity })
  lines.on('error', () => socket.destroy())
  for await (const line of lines) {
    if (message && line !== '.') { message.push(line); continue }
    if (message) {
      if (recipient && message.includes('Subject: Reset your Farm Monitoring password')) {
        writeFileSync(mailbox, message.join('\n'), { mode: 0o600 })
      }
      message = undefined
    } else if (line === 'DATA') {
      message = []
      socket.write('354 Send message\r\n')
      continue
    } else if (line === 'QUIT') {
      socket.end('221 Bye\r\n')
      return
    } else if (line.startsWith('RCPT TO:')) {
      recipient = line.toLowerCase() === 'rcpt to:<password-browser@example.com>'
    }
    socket.write('250 OK\r\n')
  }
}

export default async function setup() {
  if (process.env.Email__Enabled !== 'true') return
  if (process.env.Email__Host !== '127.0.0.1' || process.env.E2E_API_URL !== 'http://127.0.0.1:5189') throw new Error('Use the disposable browser harness.')
  const directory = mkdtempSync(join(tmpdir(), 'farm-browser-mail-'))
  const mailbox = join(directory, 'password.eml')
  process.env.E2E_RESET_MAIL = mailbox
  const server = createServer(socket => { receive(socket, mailbox).catch(() => socket.destroy()) })
  const cleanup = () => { rmSync(mailbox, { force: true }); rmdirSync(directory) }
  try {
    server.listen(Number(process.env.Email__Port), '127.0.0.1')
    await once(server, 'listening')
  } catch (error) { cleanup(); throw error }
  return async () => {
    await new Promise(resolve => server.close(resolve))
    cleanup()
  }
}
