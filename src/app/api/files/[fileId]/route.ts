import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { prisma } from '@/lib/prisma'
import { deleteS3Object } from '@/lib/s3'

export async function DELETE(
  req: NextRequest,
  { params }: { params: { fileId: string } }
) {
  const session = await getServerSession(authOptions)
  if (!session?.user?.id) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  }

  const file = await prisma.file.findFirst({
    where: { id: params.fileId, userId: session.user.id },
  })

  if (!file) {
    return NextResponse.json({ error: 'File not found' }, { status: 404 })
  }

  await deleteS3Object(file.s3Key)
  await prisma.file.delete({ where: { id: file.id } })

  return NextResponse.json({ success: true })
}
