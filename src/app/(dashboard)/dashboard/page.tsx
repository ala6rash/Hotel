import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { prisma } from '@/lib/prisma'
import { FileTable } from '@/components/dashboard/FileTable'
import { StatsCards } from '@/components/dashboard/StatsCards'
import { EmptyState } from '@/components/dashboard/EmptyState'
import Link from 'next/link'
import { Upload } from 'lucide-react'

export const metadata = { title: 'Dashboard' }

export default async function DashboardPage() {
  const session = await getServerSession(authOptions)!
  const userId = session!.user!.id as string

  const [files, stats] = await Promise.all([
    prisma.file.findMany({
      where: { userId },
      include: {
        shares: {
          orderBy: { createdAt: 'desc' },
        },
      },
      orderBy: { createdAt: 'desc' },
      take: 50,
    }),
    prisma.share.aggregate({
      _sum: { viewCount: true },
      _count: { id: true },
      where: { file: { userId } },
    }),
  ])

  const totalFiles = files.length
  const totalShares = stats._count.id
  const totalViews = stats._sum.viewCount || 0
  const activeShares = files.reduce(
    (acc, f) => acc + f.shares.filter((s) => s.status === 'ACTIVE').length,
    0
  )

  return (
    <div>
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
          <p className="text-gray-500 text-sm mt-1">Manage your shared documents</p>
        </div>
        <Link
          href="/upload"
          className="flex items-center gap-2 bg-brand-600 text-white text-sm font-semibold px-4 py-2.5 rounded-xl hover:bg-brand-700 transition-colors"
        >
          <Upload className="w-4 h-4" />
          Upload file
        </Link>
      </div>

      <StatsCards
        totalFiles={totalFiles}
        totalShares={totalShares}
        activeShares={activeShares}
        totalViews={totalViews}
      />

      <div className="mt-8">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Your Documents</h2>
        {files.length === 0 ? (
          <EmptyState />
        ) : (
          <FileTable files={files} />
        )}
      </div>
    </div>
  )
}
