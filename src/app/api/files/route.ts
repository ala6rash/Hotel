import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { prisma } from '@/lib/prisma'

export async function GET(req: NextRequest) {
  const session = await getServerSession(authOptions)
  if (!session?.user?.id) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  }

  const { searchParams } = new URL(req.url)
  const page = parseInt(searchParams.get('page') || '1')
  const limit = parseInt(searchParams.get('limit') || '20')
  const skip = (page - 1) * limit

  const [files, total] = await Promise.all([
    prisma.file.findMany({
      where: { userId: session.user.id },
      include: {
        shares: {
          select: {
            id: true,
            status: true,
            viewCount: true,
            lastViewedAt: true,
            expiresAt: true,
            viewOnce: true,
            recipientEmail: true,
            recipientName: true,
            revokedAt: true,
            createdAt: true,
            token: true,
          },
          orderBy: { createdAt: 'desc' },
        },
      },
      orderBy: { createdAt: 'desc' },
      skip,
      take: limit,
    }),
    prisma.file.count({ where: { userId: session.user.id } }),
  ])

  return NextResponse.json({ files, total, page, limit })
}
