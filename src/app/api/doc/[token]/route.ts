import { NextRequest, NextResponse } from 'next/server'
import { prisma } from '@/lib/prisma'
import { getViewPresignedUrl } from '@/lib/s3'
import { isShareAccessible } from '@/lib/utils'

export async function GET(
  req: NextRequest,
  { params }: { params: { token: string } }
) {
  const share = await prisma.share.findUnique({
    where: { token: params.token },
    include: {
      file: {
        select: {
          originalName: true,
          mimeType: true,
          size: true,
          s3Key: true,
        },
      },
    },
  })

  if (!share) {
    return NextResponse.json({ error: 'not_found' }, { status: 404 })
  }

  if (!isShareAccessible(share)) {
    const reason =
      share.status === 'REVOKED'
        ? 'revoked'
        : share.status === 'EXPIRED' ||
            (share.expiresAt && new Date() > share.expiresAt)
          ? 'expired'
          : share.viewOnce && share.viewCount >= 1
            ? 'view_once_used'
            : 'unavailable'

    return NextResponse.json({ error: reason }, { status: 403 })
  }

  const ip =
    req.headers.get('x-forwarded-for')?.split(',')[0]?.trim() ||
    req.headers.get('x-real-ip') ||
    'unknown'
  const userAgent = req.headers.get('user-agent') || ''

  await prisma.$transaction([
    prisma.share.update({
      where: { id: share.id },
      data: {
        viewCount: { increment: 1 },
        lastViewedAt: new Date(),
        status: share.viewOnce ? 'EXPIRED' : undefined,
      },
    }),
    prisma.accessLog.create({
      data: { shareId: share.id, ip, userAgent },
    }),
  ])

  const viewUrl = await getViewPresignedUrl(share.file.s3Key, 300)

  return NextResponse.json({
    file: {
      name: share.file.originalName,
      mimeType: share.file.mimeType,
      size: share.file.size,
      viewUrl,
    },
    share: {
      recipientEmail: share.recipientEmail,
      expiresAt: share.expiresAt,
      viewOnce: share.viewOnce,
      viewCount: share.viewCount + 1,
    },
  })
}
