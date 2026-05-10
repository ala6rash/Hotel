'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { File, Share, ShareStatus } from '@prisma/client'
import { formatBytes, formatDate, formatDateTime, getShareStatusLabel, isShareAccessible } from '@/lib/utils'
import {
  Eye, Download, Link2, ShieldOff, MoreVertical,
  CheckCircle2, Clock, XCircle, AlertCircle, Copy, Check
} from 'lucide-react'

type FileWithShares = File & { shares: Share[] }

function StatusBadge({ share }: { share: Share }) {
  const label = getShareStatusLabel(share.status, share.expiresAt, share.viewOnce, share.viewCount)
  const accessible = isShareAccessible(share)

  const styles: Record<string, string> = {
    Sent: 'bg-blue-50 text-blue-700',
    Viewed: 'bg-green-50 text-green-700',
    Revoked: 'bg-red-50 text-red-700',
    Expired: 'bg-gray-100 text-gray-500',
  }

  const icons: Record<string, React.ReactNode> = {
    Sent: <Clock className="w-3 h-3" />,
    Viewed: <CheckCircle2 className="w-3 h-3" />,
    Revoked: <XCircle className="w-3 h-3" />,
    Expired: <AlertCircle className="w-3 h-3" />,
  }

  return (
    <span className={`inline-flex items-center gap-1 text-xs font-medium px-2 py-1 rounded-full ${styles[label] || 'bg-gray-100 text-gray-500'}`}>
      {icons[label]}
      {label}
    </span>
  )
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)

  async function copy() {
    await navigator.clipboard.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <button
      onClick={copy}
      className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-all"
      title="Copy link"
    >
      {copied ? <Check className="w-3.5 h-3.5 text-green-500" /> : <Copy className="w-3.5 h-3.5" />}
    </button>
  )
}

export function FileTable({ files }: { files: FileWithShares[] }) {
  const router = useRouter()
  const [revoking, setRevoking] = useState<string | null>(null)

  const appUrl = typeof window !== 'undefined' ? window.location.origin : ''

  async function revokeShare(fileId: string, shareId: string) {
    setRevoking(shareId)
    await fetch(`/api/files/${fileId}/revoke`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ shareId }),
    })
    setRevoking(null)
    router.refresh()
  }

  return (
    <div className="bg-white rounded-2xl border border-gray-100 overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="border-b border-gray-50">
              <th className="text-left text-xs font-medium text-gray-400 uppercase tracking-wider px-6 py-4">File</th>
              <th className="text-left text-xs font-medium text-gray-400 uppercase tracking-wider px-4 py-4">Recipient</th>
              <th className="text-left text-xs font-medium text-gray-400 uppercase tracking-wider px-4 py-4">Status</th>
              <th className="text-left text-xs font-medium text-gray-400 uppercase tracking-wider px-4 py-4">Views</th>
              <th className="text-left text-xs font-medium text-gray-400 uppercase tracking-wider px-4 py-4">Sent</th>
              <th className="text-left text-xs font-medium text-gray-400 uppercase tracking-wider px-4 py-4">Expires</th>
              <th className="px-4 py-4" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {files.map((file) => {
              if (file.shares.length === 0) {
                return (
                  <tr key={file.id} className="hover:bg-gray-50/50 transition-colors">
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 bg-gray-100 rounded-lg flex items-center justify-center">
                          <Download className="w-4 h-4 text-gray-400" />
                        </div>
                        <div>
                          <p className="text-sm font-medium text-gray-900 max-w-[200px] truncate">{file.originalName}</p>
                          <p className="text-xs text-gray-400">{formatBytes(file.size)}</p>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-4 text-sm text-gray-400">—</td>
                    <td className="px-4 py-4">
                      <span className="text-xs text-gray-400 bg-gray-50 px-2 py-1 rounded-full">Not shared</span>
                    </td>
                    <td className="px-4 py-4 text-sm text-gray-400">0</td>
                    <td className="px-4 py-4 text-sm text-gray-400">{formatDate(file.createdAt)}</td>
                    <td className="px-4 py-4 text-sm text-gray-400">—</td>
                    <td className="px-4 py-4">
                      <a
                        href={`/upload?fileId=${file.id}`}
                        className="text-xs text-brand-600 hover:text-brand-700 font-medium"
                      >
                        Send
                      </a>
                    </td>
                  </tr>
                )
              }

              return file.shares.map((share, si) => (
                <tr key={share.id} className="hover:bg-gray-50/50 transition-colors">
                  {si === 0 && (
                    <td className="px-6 py-4" rowSpan={file.shares.length}>
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 bg-brand-50 rounded-lg flex items-center justify-center">
                          <Download className="w-4 h-4 text-brand-600" />
                        </div>
                        <div>
                          <p className="text-sm font-medium text-gray-900 max-w-[200px] truncate">{file.originalName}</p>
                          <p className="text-xs text-gray-400">{formatBytes(file.size)}</p>
                        </div>
                      </div>
                    </td>
                  )}
                  <td className="px-4 py-4">
                    <div>
                      <p className="text-sm text-gray-900">{share.recipientName || share.recipientEmail || 'Anonymous'}</p>
                      {share.recipientName && share.recipientEmail && (
                        <p className="text-xs text-gray-400">{share.recipientEmail}</p>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-4"><StatusBadge share={share} /></td>
                  <td className="px-4 py-4">
                    <div className="flex items-center gap-1.5 text-sm text-gray-600">
                      <Eye className="w-3.5 h-3.5 text-gray-400" />
                      {share.viewCount}
                      {share.lastViewedAt && (
                        <span className="text-xs text-gray-400">· {formatDateTime(share.lastViewedAt)}</span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-4 text-sm text-gray-500">{formatDate(share.createdAt)}</td>
                  <td className="px-4 py-4 text-sm text-gray-500">
                    {share.expiresAt ? formatDate(share.expiresAt) : share.viewOnce ? 'One-time' : '—'}
                  </td>
                  <td className="px-4 py-4">
                    <div className="flex items-center gap-1">
                      <CopyButton text={`${appUrl}/d/${share.token}`} />
                      <a
                        href={`/d/${share.token}`}
                        target="_blank"
                        className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-all"
                        title="View document"
                      >
                        <Link2 className="w-3.5 h-3.5" />
                      </a>
                      {share.status === 'ACTIVE' && (
                        <button
                          onClick={() => revokeShare(file.id, share.id)}
                          disabled={revoking === share.id}
                          className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-all disabled:opacity-50"
                          title="Revoke access"
                        >
                          <ShieldOff className="w-3.5 h-3.5" />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
