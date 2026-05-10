import { NextRequest, NextResponse } from 'next/server'
import { prisma } from '@/lib/prisma'
import { getDownloadPresignedUrl } from '@/lib/s3'
import { isShareAccessible } from '@/lib/utils'

export async function GET(
  req: NextRequest,
  { params }: { params: { token: string } }
) {
  const share = await prisma.share.findUnique({
    where: { token: params.token },
    include: { file: true },
  })

  if (!share || !isShareAccessible(share)) {
    return NextResponse.json({ error: 'Access denied' }, { status: 403 })
  }

  const downloadUrl = await getDownloadPresignedUrl(
    share.file.s3Key,
    share.file.originalName,
    300
  )

  return NextResponse.redirect(downloadUrl)
}
