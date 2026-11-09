import { ImageResponse } from 'next/og'

// The picture a link preview shows (WhatsApp, Telegram, Slack...). One picture for the whole site, drawn from fixed text:
// it never shows a stokvel's name or an amount, so it is safe on the invite page too.
export const alt = 'RondiTrack: run your stokvel with trust'
export const size = { width: 1200, height: 630 }
export const contentType = 'image/png'

export default function OpenGraphImage() {
  return new ImageResponse(
    (
      <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'center', padding: 80, background: '#0F3D32', color: '#ffffff' }}>
        <div style={{ fontSize: 40, fontWeight: 700, color: '#B9D3C9' }}>RondiTrack</div>
        <div style={{ marginTop: 24, fontSize: 84, fontWeight: 800, lineHeight: 1.1 }}>Run your stokvel with trust.</div>
        <div style={{ marginTop: 32, fontSize: 36, color: '#D5E4DE' }}>Contributions, payouts and proof of payment in one place.</div>
      </div>
    ),
    size,
  )
}
