import { Resend } from 'resend'

const resend = new Resend(process.env.RESEND_API_KEY)

const APP_NAME = process.env.NEXT_PUBLIC_APP_NAME || 'SecureDocs'
const APP_URL = process.env.NEXT_PUBLIC_APP_URL || 'http://localhost:3000'
const FROM = process.env.EMAIL_FROM || 'noreply@securedocs.app'

export async function sendDocumentShareEmail({
  recipientEmail,
  recipientName,
  senderName,
  fileName,
  token,
  message,
  expiresAt,
}: {
  recipientEmail: string
  recipientName?: string | null
  senderName: string
  fileName: string
  token: string
  message?: string | null
  expiresAt?: Date | null
}) {
  const shareUrl = `${APP_URL}/d/${token}`
  const greeting = recipientName ? `Hi ${recipientName},` : 'Hello,'
  const expiry = expiresAt
    ? `<p style="color:#6b7280;font-size:14px;">This link expires on ${expiresAt.toLocaleDateString('en-US', { dateStyle: 'long' })}.</p>`
    : ''

  await resend.emails.send({
    from: `${APP_NAME} <${FROM}>`,
    to: recipientEmail,
    subject: `${senderName} has shared a document with you`,
    html: `
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
</head>
<body style="font-family:Inter,system-ui,sans-serif;background:#f9fafb;margin:0;padding:40px 20px;">
  <div style="max-width:560px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.1);">
    <div style="background:#4f46e5;padding:32px 40px;">
      <h1 style="color:#fff;margin:0;font-size:24px;font-weight:700;">${APP_NAME}</h1>
    </div>
    <div style="padding:40px;">
      <p style="color:#111827;font-size:16px;margin-top:0;">${greeting}</p>
      <p style="color:#374151;font-size:16px;"><strong>${senderName}</strong> has shared a secure document with you.</p>
      <div style="background:#f3f4f6;border-radius:8px;padding:16px;margin:24px 0;">
        <p style="margin:0;color:#6b7280;font-size:12px;text-transform:uppercase;letter-spacing:0.05em;font-weight:600;">Document</p>
        <p style="margin:4px 0 0;color:#111827;font-size:16px;font-weight:600;">${fileName}</p>
      </div>
      ${message ? `<div style="border-left:3px solid #4f46e5;padding:12px 16px;margin:24px 0;"><p style="margin:0;color:#374151;font-size:14px;font-style:italic;">"${message}"</p></div>` : ''}
      <a href="${shareUrl}" style="display:inline-block;background:#4f46e5;color:#fff;text-decoration:none;padding:14px 28px;border-radius:8px;font-size:16px;font-weight:600;margin:8px 0;">View Document →</a>
      <p style="color:#9ca3af;font-size:12px;margin-top:24px;">Or copy this link: <a href="${shareUrl}" style="color:#4f46e5;">${shareUrl}</a></p>
      ${expiry}
      <hr style="border:none;border-top:1px solid #e5e7eb;margin:32px 0;">
      <p style="color:#9ca3af;font-size:12px;margin:0;">This document was shared via ${APP_NAME}. If you believe this was sent in error, you can ignore this email.</p>
    </div>
  </div>
</body>
</html>
    `.trim(),
  })
}

export async function sendPasswordResetEmail({
  email,
  token,
}: {
  email: string
  token: string
}) {
  const resetUrl = `${APP_URL}/reset-password?token=${token}`

  await resend.emails.send({
    from: `${APP_NAME} <${FROM}>`,
    to: email,
    subject: 'Reset your password',
    html: `
<!DOCTYPE html>
<html>
<body style="font-family:Inter,system-ui,sans-serif;background:#f9fafb;margin:0;padding:40px 20px;">
  <div style="max-width:560px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.1);">
    <div style="background:#4f46e5;padding:32px 40px;">
      <h1 style="color:#fff;margin:0;font-size:24px;font-weight:700;">${APP_NAME}</h1>
    </div>
    <div style="padding:40px;">
      <h2 style="color:#111827;margin-top:0;">Reset your password</h2>
      <p style="color:#374151;">We received a request to reset your password. Click the button below to create a new password. This link expires in 1 hour.</p>
      <a href="${resetUrl}" style="display:inline-block;background:#4f46e5;color:#fff;text-decoration:none;padding:14px 28px;border-radius:8px;font-size:16px;font-weight:600;margin:8px 0;">Reset Password</a>
      <p style="color:#9ca3af;font-size:12px;margin-top:24px;">If you didn't request this, you can safely ignore this email.</p>
    </div>
  </div>
</body>
</html>
    `.trim(),
  })
}
