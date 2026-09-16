import { createPrivateKey, createPublicKey, X509Certificate } from 'node:crypto'
import { readFileSync, statSync } from 'node:fs'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

function loadHttpsOptions(mode: string) {
  const env = loadEnv(mode, process.cwd(), '')
  const certificatePath = process.env.VITE_DEV_HTTPS_CERT?.trim() || env.VITE_DEV_HTTPS_CERT?.trim()
  const keyPath = process.env.VITE_DEV_HTTPS_KEY?.trim() || env.VITE_DEV_HTTPS_KEY?.trim()

  if (!certificatePath || !keyPath) {
    throw new Error('Vite HTTPS requiere VITE_DEV_HTTPS_CERT y VITE_DEV_HTTPS_KEY.')
  }

  let certificate: Buffer
  let privateKeyBytes: Buffer
  let privateKey: ReturnType<typeof createPrivateKey>

  try {
    if (!statSync(certificatePath).isFile() || !statSync(keyPath).isFile()) {
      throw new Error('certificate files are unavailable')
    }

    certificate = readFileSync(certificatePath)
    privateKeyBytes = readFileSync(keyPath)
    const certificatePublicKey = new X509Certificate(certificate).publicKey
    privateKey = createPrivateKey(privateKeyBytes)
    const privatePublicKey = createPublicKey(privateKey)

    const certificateDer = certificatePublicKey.export({ format: 'der', type: 'spki' })
    const privateDer = privatePublicKey.export({ format: 'der', type: 'spki' })

    if (!certificateDer.equals(privateDer)) {
      throw new Error('certificate and key do not match')
    }
  } catch {
    throw new Error('Vite HTTPS requiere un certificado y una llave privada PEM válidos.')
  }

  return { cert: certificate, key: privateKeyBytes }
}

export default defineConfig(({ command, mode }) => {
  const https = command === 'serve' ? loadHttpsOptions(mode) : undefined
  const cacheDir = process.env.UCREDIT_VITE_CACHE_DIR?.trim()

  return {
    // Keep certificate paths server-only while exposing only the optional browser API URL.
    envPrefix: ['VITE_API_BASE_URL'],
    cacheDir: command === 'serve' ? cacheDir : undefined,
    plugins: [react()],
    server: {
      host: 'localhost',
      port: 5173,
      strictPort: true,
      https,
      proxy: {
        '/api': {
          target: 'https://localhost:7042',
          changeOrigin: false,
          secure: false,
        },
        '/health': {
          target: 'https://localhost:7042',
          changeOrigin: false,
          secure: false,
        },
      },
    },
  }
})
