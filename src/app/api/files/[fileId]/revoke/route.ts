import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { prisma } from '@/lib/prisma'

export async function POST(
  req: NextRequest,
  { params }: { params: { fileId: string } }
) {
  const session = await getServerSession(authOptions)
  if (!session?.user?.id) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  }

  const body = await req.json().catch(() => ({}))
  const shareId = body.shareId as string | undefined

  const file = await prisma.file.findFirst({
    where: { id: params.fileId, userId: session.user.id },
  })

  if (!file) {
    return NextResponse.json({ error: 'File not found' }, { status: 404 })
  }

  if (shareId) {
    await prisma.share.updateMany({
      where: { id: shareId, fileId: file.id },
      data: { status: 'REVOKED', revokedAt: new Date() },
    })
  } else {
    await prisma.share.updateMany({
      where: { fileId: file.id, status: 'ACTIVE' },
      data: { status: 'REVOKED', revokedAt: new Date() },
    })
  }

  return NextResponse.json({ success: true })
}
