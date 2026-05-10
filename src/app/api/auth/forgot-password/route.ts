import { NextRequest, NextResponse } from 'next/server'
import { prisma } from '@/lib/prisma'
import { sendPasswordResetEmail } from '@/lib/email'
import { nanoid } from 'nanoid'
import { z } from 'zod'

export async function POST(req: NextRequest) {
  try {
    const { email } = z.object({ email: z.string().email() }).parse(await req.json())

    const user = await prisma.user.findUnique({
      where: { email: email.toLowerCase() },
    })

    if (user) {
      const token = nanoid(32)
      const expires = new Date(Date.now() + 60 * 60 * 1000) // 1 hour

      await prisma.verificationToken.upsert({
        where: { identifier: `reset-${user.email}` },
        create: { identifier: `reset-${user.email}`, token, expires },
        update: { token, expires },
      })

      await sendPasswordResetEmail({ email: user.email, token })
    }

    return NextResponse.json({ success: true })
  } catch (err) {
    if (err instanceof z.ZodError) {
      return NextResponse.json({ error: 'Invalid email' }, { status: 400 })
    }
    console.error('Forgot password error:', err)
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 })
  }
}
