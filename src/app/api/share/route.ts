import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { prisma } from '@/lib/prisma'
import { sendDocumentShareEmail } from '@/lib/email'
import { z } from 'zod'
import { addDays } from 'date-fns'

const shareSchema = z.object({
  fileId: z.string(),
  recipientEmail: z.string().email().optional(),
  recipientName: z.string().max(100).optional(),
  message: z.string().max(500).optional(),
  expiryType: z.enum(['NEVER', 'DAYS', 'VIEW_ONCE']).default('NEVER'),
  expiryDays: z.number().int().min(1).max(365).optional(),
  sendEmail: z.boolean().default(false),
})

export async function POST(req: NextRequest) {
  const session = await getServerSession(authOptions)
  if (!session?.user?.id) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  }

  try {
    const body = await req.json()
    const data = shareSchema.parse(body)

    const file = await prisma.file.findFirst({
      where: { id: data.fileId, userId: session.user.id },
    })

    if (!file) {
      return NextResponse.json({ error: 'File not found' }, { status: 404 })
    }

    let expiresAt: Date | null = null
    if (data.expiryType === 'DAYS' && data.expiryDays) {
      expiresAt = addDays(new Date(), data.expiryDays)
    }

    const share = await prisma.share.create({
      data: {
        fileId: file.id,
        recipientEmail: data.recipientEmail || null,
        recipientName: data.recipientName || null,
        message: data.message || null,
        expiryType: data.expiryType,
        expiryDays: data.expiryDays || null,
        expiresAt,
        viewOnce: data.expiryType === 'VIEW_ONCE',
        emailSent: false,
      },
    })

    if (data.sendEmail && data.recipientEmail) {
      try {
        await sendDocumentShareEmail({
          recipientEmail: data.recipientEmail,
          recipientName: data.recipientName,
          senderName: session.user.name || session.user.email || 'Someone',
          fileName: file.originalName,
          token: share.token,
          message: data.message,
          expiresAt,
        })
        await prisma.share.update({
          where: { id: share.id },
          data: { emailSent: true, emailSentAt: new Date() },
        })
      } catch (emailErr) {
        console.error('Email send failed:', emailErr)
      }
    }

    const appUrl = process.env.NEXT_PUBLIC_APP_URL || 'http://localhost:3000'
    return NextResponse.json({
      shareId: share.id,
      token: share.token,
      shareUrl: `${appUrl}/d/${share.token}`,
    })
  } catch (err) {
    if (err instanceof z.ZodError) {
      return NextResponse.json({ error: err.errors[0].message }, { status: 400 })
    }
    console.error('Share error:', err)
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 })
  }
}
