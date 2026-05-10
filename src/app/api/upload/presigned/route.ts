import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { getUploadPresignedUrl, generateS3Key } from '@/lib/s3'
import { prisma } from '@/lib/prisma'
import { z } from 'zod'
import { ALLOWED_MIME_TYPES, MAX_FILE_SIZE } from '@/lib/utils'

const uploadSchema = z.object({
  filename: z.string().min(1).max(255),
  contentType: z.string(),
  size: z.number().int().positive().max(MAX_FILE_SIZE),
})

export async function POST(req: NextRequest) {
  const session = await getServerSession(authOptions)
  if (!session?.user?.id) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  }

  try {
    const body = await req.json()
    const { filename, contentType, size } = uploadSchema.parse(body)

    if (!ALLOWED_MIME_TYPES.includes(contentType)) {
      return NextResponse.json({ error: 'File type not allowed' }, { status: 400 })
    }

    const s3Key = generateS3Key(session.user.id, filename)
    const presignedUrl = await getUploadPresignedUrl(s3Key, contentType, size)

    const file = await prisma.file.create({
      data: {
        originalName: filename,
        storedName: filename,
        size,
        mimeType: contentType,
        s3Key,
        userId: session.user.id,
      },
    })

    return NextResponse.json({ presignedUrl, fileId: file.id })
  } catch (err) {
    if (err instanceof z.ZodError) {
      return NextResponse.json({ error: err.errors[0].message }, { status: 400 })
    }
    console.error('Presigned URL error:', err)
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 })
  }
}
