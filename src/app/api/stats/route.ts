import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { prisma } from '@/lib/prisma'

export async function GET(req: NextRequest) {
  const session = await getServerSession(authOptions)
  if (!session?.user?.id) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  }

  const [totalFiles, totalShares, activeShares, totalViews] = await Promise.all([
    prisma.file.count({ where: { userId: session.user.id } }),
    prisma.share.count({
      where: { file: { userId: session.user.id } },
    }),
    prisma.share.count({
      where: { file: { userId: session.user.id }, status: 'ACTIVE' },
    }),
    prisma.share.aggregate({
      _sum: { viewCount: true },
      where: { file: { userId: session.user.id } },
    }),
  ])

  return NextResponse.json({
    totalFiles,
    totalShares,
    activeShares,
    totalViews: totalViews._sum.viewCount || 0,
  })
}
