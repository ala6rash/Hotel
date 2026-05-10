'use client'

import { useEffect, useState } from 'react'
import { Download, Shield, Clock, AlertTriangle, XCircle, Eye, Lock } from 'lucide-react'
import Link from 'next/link'

interface DocData {
  file: {
    name: string
    mimeType: string
    size: number
    viewUrl: string
  }
  share: {
    recipientEmail: string | null
    expiresAt: string | null
    viewOnce: boolean
    viewCount: number
  }
}

type ErrorType = 'revoked' | 'expired' | 'view_once_used' | 'not_found' | 'unavailable'

function ErrorPage({ type }: { type: ErrorType }) {
  const config: Record<ErrorType, { icon: React.ReactNode; title: string; desc: string; bg: string }> = {
    revoked: {
      icon: <XCircle className="w-10 h-10 text-red-400" />,
      title: 'Access revoked',
      desc: 'The sender has revoked access to this document. It is no longer available.',
      bg: 'bg-red-50',
    },
    expired: {
      icon: <Clock className="w-10 h-10 text-yellow-400" />,
      title: 'Link expired',
      desc: 'This share link has expired and can no longer be used to access the document.',
      bg: 'bg-yellow-50',
    },
    view_once_used: {
      icon: <Eye className="w-10 h-10 text-blue-400" />,
      title: 'Already viewed',
      desc: 'This document was shared as a one-time view. It has already been accessed.',
      bg: 'bg-blue-50',
    },
    not_found: {
      icon: <AlertTriangle className="w-10 h-10 text-gray-400" />,
      title: 'Document not found',
      desc: 'This link does not exist or has been removed.',
      bg: 'bg-gray-50',
    },
    unavailable: {
      icon: <AlertTriangle className="w-10 h-10 text-gray-400" />,
      title: 'Document unavailable',
      desc: 'This document is no longer available.',
      bg: 'bg-gray-50',
    },
  }

  const c = config[type]

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-md w-full text-center">
        <div className={`w-20 h-20 ${c.bg} rounded-full flex items-center justify-center mx-auto mb-6`}>
          {c.icon}
        </div>
        <h1 className="text-2xl font-bold text-gray-900 mb-3">{c.title}</h1>
        <p className="text-gray-500 mb-8">{c.desc}</p>
        <div className="flex items-center justify-center gap-2 text-sm text-gray-400">
          <Lock className="w-4 h-4" />
          <span>Protected by SecureDocs</span>
        </div>
      </div>
    </div>
  )
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
}

export default function DocumentViewPage({ params }: { params: { token: string } }) {
  const [doc, setDoc] = useState<DocData | null>(null)
  const [errorType, setErrorType] = useState<ErrorType | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch(`/api/doc/${params.token}`)
      .then(async (res) => {
        if (!res.ok) {
          const data = await res.json()
          setErrorType((data.error as ErrorType) || 'unavailable')
        } else {
          setDoc(await res.json())
        }
      })
      .catch(() => setErrorType('unavailable'))
      .finally(() => setLoading(false))
  }, [params.token])

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="flex flex-col items-center gap-4">
          <div className="w-10 h-10 border-3 border-brand-200 border-t-brand-600 rounded-full animate-spin" />
          <p className="text-gray-500 text-sm">Loading document...</p>
        </div>
      </div>
    )
  }

  if (errorType) return <ErrorPage type={errorType} />
  if (!doc) return <ErrorPage type="unavailable" />

  const isPdf = doc.file.mimeType === 'application/pdf'
  const isImage = doc.file.mimeType.startsWith('image/')

  return (
    <div className="min-h-screen bg-gray-900 flex flex-col">
      {/* Header */}
      <header className="bg-gray-900 border-b border-gray-800 px-6 py-4 flex items-center justify-between flex-shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-brand-600 rounded-lg flex items-center justify-center">
            <Lock className="w-4 h-4 text-white" />
          </div>
          <div>
            <p className="text-sm font-semibold text-white leading-none">{doc.file.name}</p>
            <p className="text-xs text-gray-400 mt-0.5">{formatBytes(doc.file.size)}</p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {doc.share.expiresAt && (
            <div className="hidden sm:flex items-center gap-1.5 text-xs text-yellow-400 bg-yellow-400/10 px-3 py-1.5 rounded-full">
              <Clock className="w-3 h-3" />
              Expires {new Date(doc.share.expiresAt).toLocaleDateString()}
            </div>
          )}
          {doc.share.viewOnce && (
            <div className="hidden sm:flex items-center gap-1.5 text-xs text-blue-400 bg-blue-400/10 px-3 py-1.5 rounded-full">
              <Eye className="w-3 h-3" />
              One-time view
            </div>
          )}
          <a
            href={`/api/doc/${params.token}/download`}
            className="flex items-center gap-2 bg-brand-600 text-white text-sm font-medium px-4 py-2 rounded-lg hover:bg-brand-700 transition-colors"
          >
            <Download className="w-4 h-4" />
            Download
          </a>
        </div>
      </header>

      {/* Document viewer */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {isPdf && (
          <iframe
            src={`${doc.file.viewUrl}#toolbar=0&navpanes=0`}
            className="flex-1 w-full border-0"
            title={doc.file.name}
          />
        )}
        {isImage && (
          <div className="flex-1 flex items-center justify-center p-8">
            {/* Watermark overlay */}
            <div className="relative inline-block max-w-full max-h-full">
              <img
                src={doc.file.viewUrl}
                alt={doc.file.name}
                className="max-w-full max-h-[80vh] object-contain rounded-lg shadow-2xl"
                style={{ userSelect: 'none' }}
                draggable={false}
              />
              {doc.share.recipientEmail && (
                <div
                  className="absolute inset-0 pointer-events-none flex items-center justify-center"
                  style={{ transform: 'rotate(-30deg)' }}
                >
                  <p className="text-white/10 text-2xl font-bold whitespace-nowrap select-none">
                    {doc.share.recipientEmail} · {new Date().toLocaleDateString()}
                  </p>
                </div>
              )}
            </div>
          </div>
        )}
        {!isPdf && !isImage && (
          <div className="flex-1 flex flex-col items-center justify-center p-8 text-center">
            <div className="w-20 h-20 bg-gray-800 rounded-2xl flex items-center justify-center mb-6">
              <Shield className="w-10 h-10 text-gray-500" />
            </div>
            <h2 className="text-xl font-semibold text-white mb-2">Preview not available</h2>
            <p className="text-gray-400 text-sm mb-6 max-w-xs">
              This file type cannot be previewed in the browser. Click below to download it.
            </p>
            <a
              href={`/api/doc/${params.token}/download`}
              className="flex items-center gap-2 bg-brand-600 text-white font-semibold px-6 py-3 rounded-xl hover:bg-brand-700 transition-colors"
            >
              <Download className="w-5 h-5" />
              Download document
            </a>
          </div>
        )}
      </div>

      {/* Footer */}
      <footer className="bg-gray-900 border-t border-gray-800 px-6 py-3 flex items-center justify-between">
        <div className="flex items-center gap-2 text-xs text-gray-500">
          <Shield className="w-3.5 h-3.5" />
          <span>Securely delivered via SecureDocs</span>
        </div>
        <Link href="/" className="text-xs text-gray-600 hover:text-gray-400 transition-colors">
          securedocs.app
        </Link>
      </footer>
    </div>
  )
}
