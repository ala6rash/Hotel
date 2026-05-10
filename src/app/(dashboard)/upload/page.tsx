'use client'

import { useState, useCallback } from 'react'
import { useDropzone } from 'react-dropzone'
import { useRouter } from 'next/navigation'
import {
  Upload, FileText, X, CheckCircle2, Loader2, Copy, Check,
  Send, Clock, Eye, ShieldOff, AlertCircle
} from 'lucide-react'
import { formatBytes, ALLOWED_MIME_TYPES, MAX_FILE_SIZE } from '@/lib/utils'

type Step = 'upload' | 'configure' | 'done'

interface UploadedFile {
  id: string
  name: string
  size: number
  type: string
}

interface ShareResult {
  shareUrl: string
  shareId: string
  token: string
}

export default function UploadPage() {
  const router = useRouter()
  const [step, setStep] = useState<Step>('upload')
  const [file, setFile] = useState<File | null>(null)
  const [uploadedFile, setUploadedFile] = useState<UploadedFile | null>(null)
  const [uploading, setUploading] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)
  const [error, setError] = useState('')

  // Send form state
  const [recipientEmail, setRecipientEmail] = useState('')
  const [recipientName, setRecipientName] = useState('')
  const [message, setMessage] = useState('')
  const [expiryType, setExpiryType] = useState<'NEVER' | 'DAYS' | 'VIEW_ONCE'>('NEVER')
  const [expiryDays, setExpiryDays] = useState(7)
  const [sendEmail, setSendEmail] = useState(false)
  const [sending, setSending] = useState(false)
  const [shareResult, setShareResult] = useState<ShareResult | null>(null)
  const [copied, setCopied] = useState(false)

  const onDrop = useCallback((accepted: File[], rejected: any[]) => {
    setError('')
    if (rejected.length > 0) {
      const r = rejected[0]
      if (r.errors[0]?.code === 'file-too-large') {
        setError('File is too large. Maximum size is 100MB.')
      } else if (r.errors[0]?.code === 'file-invalid-type') {
        setError('File type not allowed. Supported: PDF, Word, Excel, images, ZIP.')
      } else {
        setError('File rejected: ' + r.errors[0]?.message)
      }
      return
    }
    if (accepted[0]) setFile(accepted[0])
  }, [])

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    maxFiles: 1,
    maxSize: MAX_FILE_SIZE,
    accept: ALLOWED_MIME_TYPES.reduce((acc, mime) => ({ ...acc, [mime]: [] }), {}),
  })

  async function handleUpload() {
    if (!file) return
    setUploading(true)
    setError('')

    try {
      const presignedRes = await fetch('/api/upload/presigned', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filename: file.name, contentType: file.type, size: file.size }),
      })

      if (!presignedRes.ok) {
        const d = await presignedRes.json()
        throw new Error(d.error || 'Failed to get upload URL')
      }

      const { presignedUrl, fileId } = await presignedRes.json()

      const xhr = new XMLHttpRequest()
      await new Promise<void>((resolve, reject) => {
        xhr.upload.onprogress = (e) => {
          if (e.lengthComputable) setUploadProgress(Math.round((e.loaded / e.total) * 100))
        }
        xhr.onload = () => (xhr.status === 200 ? resolve() : reject(new Error('Upload failed')))
        xhr.onerror = () => reject(new Error('Upload failed'))
        xhr.open('PUT', presignedUrl)
        xhr.setRequestHeader('Content-Type', file.type)
        xhr.send(file)
      })

      setUploadedFile({ id: fileId, name: file.name, size: file.size, type: file.type })
      setStep('configure')
    } catch (err: any) {
      setError(err.message || 'Upload failed. Please try again.')
    } finally {
      setUploading(false)
      setUploadProgress(0)
    }
  }

  async function handleSend() {
    if (!uploadedFile) return
    setSending(true)
    setError('')

    try {
      const res = await fetch('/api/share', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fileId: uploadedFile.id,
          recipientEmail: recipientEmail || undefined,
          recipientName: recipientName || undefined,
          message: message || undefined,
          expiryType,
          expiryDays: expiryType === 'DAYS' ? expiryDays : undefined,
          sendEmail: sendEmail && !!recipientEmail,
        }),
      })

      if (!res.ok) {
        const d = await res.json()
        throw new Error(d.error || 'Failed to create share link')
      }

      const data = await res.json()
      setShareResult(data)
      setStep('done')
    } catch (err: any) {
      setError(err.message || 'Something went wrong.')
    } finally {
      setSending(false)
    }
  }

  async function copyLink() {
    if (!shareResult) return
    await navigator.clipboard.writeText(shareResult.shareUrl)
    setCopied(true)
    setTimeout(() => setCopied(false), 2500)
  }

  return (
    <div className="max-w-2xl mx-auto">
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Upload & Send</h1>
        <p className="text-gray-500 text-sm mt-1">Upload a file and generate a secure share link</p>
      </div>

      {/* Steps indicator */}
      <div className="flex items-center gap-3 mb-8">
        {(['upload', 'configure', 'done'] as Step[]).map((s, i) => {
          const stepLabels = ['Upload file', 'Configure share', 'Share link']
          const isDone = (step === 'configure' && i === 0) || (step === 'done' && i <= 1)
          const isActive = step === s
          return (
            <div key={s} className="flex items-center gap-2">
              <div className={`flex items-center gap-2 text-sm font-medium transition-colors ${
                isActive ? 'text-brand-700' : isDone ? 'text-green-600' : 'text-gray-400'
              }`}>
                <div className={`w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold ${
                  isDone ? 'bg-green-100 text-green-600' :
                  isActive ? 'bg-brand-100 text-brand-700' :
                  'bg-gray-100 text-gray-400'
                }`}>
                  {isDone ? <CheckCircle2 className="w-3.5 h-3.5" /> : i + 1}
                </div>
                {stepLabels[i]}
              </div>
              {i < 2 && <div className="w-8 h-px bg-gray-200" />}
            </div>
          )
        })}
      </div>

      {error && (
        <div className="flex items-start gap-3 bg-red-50 border border-red-200 text-red-700 text-sm px-4 py-3 rounded-xl mb-6">
          <AlertCircle className="w-4 h-4 mt-0.5 flex-shrink-0" />
          {error}
        </div>
      )}

      {/* Step 1: Upload */}
      {step === 'upload' && (
        <div className="bg-white rounded-2xl border border-gray-100 p-6">
          {!file ? (
            <div
              {...getRootProps()}
              className={`border-2 border-dashed rounded-xl p-12 text-center cursor-pointer transition-all ${
                isDragActive ? 'border-brand-400 bg-brand-50' : 'border-gray-200 hover:border-brand-300 hover:bg-gray-50'
              }`}
            >
              <input {...getInputProps()} />
              <div className="w-14 h-14 bg-brand-50 rounded-2xl flex items-center justify-center mx-auto mb-4">
                <Upload className={`w-7 h-7 ${isDragActive ? 'text-brand-600' : 'text-brand-400'}`} />
              </div>
              <p className="text-gray-700 font-semibold mb-1">
                {isDragActive ? 'Drop your file here' : 'Drag & drop your file'}
              </p>
              <p className="text-gray-400 text-sm mb-4">or click to browse</p>
              <p className="text-xs text-gray-400">PDF, Word, Excel, PowerPoint, Images, ZIP · Max 100MB</p>
            </div>
          ) : (
            <div>
              <div className="flex items-center gap-4 p-4 bg-gray-50 rounded-xl mb-6">
                <div className="w-10 h-10 bg-brand-50 rounded-xl flex items-center justify-center">
                  <FileText className="w-5 h-5 text-brand-600" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-gray-900 truncate">{file.name}</p>
                  <p className="text-xs text-gray-400">{formatBytes(file.size)}</p>
                </div>
                {!uploading && (
                  <button
                    onClick={() => setFile(null)}
                    className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-200 rounded-lg transition-all"
                  >
                    <X className="w-4 h-4" />
                  </button>
                )}
              </div>

              {uploading && (
                <div className="mb-6">
                  <div className="flex justify-between text-xs text-gray-500 mb-1">
                    <span>Uploading...</span>
                    <span>{uploadProgress}%</span>
                  </div>
                  <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-brand-500 rounded-full transition-all duration-300"
                      style={{ width: `${uploadProgress}%` }}
                    />
                  </div>
                </div>
              )}

              <button
                onClick={handleUpload}
                disabled={uploading}
                className="w-full flex items-center justify-center gap-2 bg-brand-600 text-white font-semibold py-3 rounded-xl hover:bg-brand-700 disabled:opacity-60 disabled:cursor-not-allowed transition-colors"
              >
                {uploading ? (
                  <><Loader2 className="w-4 h-4 animate-spin" /> Uploading...</>
                ) : (
                  <><Upload className="w-4 h-4" /> Upload file</>
                )}
              </button>
            </div>
          )}
        </div>
      )}

      {/* Step 2: Configure */}
      {step === 'configure' && uploadedFile && (
        <div className="bg-white rounded-2xl border border-gray-100 p-6 space-y-6">
          <div className="flex items-center gap-3 p-4 bg-green-50 rounded-xl">
            <CheckCircle2 className="w-5 h-5 text-green-500" />
            <div>
              <p className="text-sm font-medium text-gray-900">File uploaded successfully</p>
              <p className="text-xs text-gray-500">{uploadedFile.name} · {formatBytes(uploadedFile.size)}</p>
            </div>
          </div>

          <div className="grid sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">
                Recipient name <span className="text-gray-400 font-normal">(optional)</span>
              </label>
              <input
                type="text"
                value={recipientName}
                onChange={(e) => setRecipientName(e.target.value)}
                className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition-all text-sm"
                placeholder="Jane Smith"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">
                Recipient email <span className="text-gray-400 font-normal">(optional)</span>
              </label>
              <input
                type="email"
                value={recipientEmail}
                onChange={(e) => setRecipientEmail(e.target.value)}
                className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition-all text-sm"
                placeholder="jane@example.com"
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              Personal message <span className="text-gray-400 font-normal">(optional)</span>
            </label>
            <textarea
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              rows={3}
              maxLength={500}
              className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition-all text-sm resize-none"
              placeholder="Add a note for the recipient..."
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-3">Link expiry</label>
            <div className="grid grid-cols-3 gap-3">
              {([
                { value: 'NEVER', label: 'Never', icon: ShieldOff, desc: 'No expiry' },
                { value: 'DAYS', label: 'After X days', icon: Clock, desc: 'Time-limited' },
                { value: 'VIEW_ONCE', label: 'View once', icon: Eye, desc: 'Auto-expires' },
              ] as const).map((opt) => (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setExpiryType(opt.value)}
                  className={`p-3 rounded-xl border text-left transition-all ${
                    expiryType === opt.value
                      ? 'border-brand-400 bg-brand-50'
                      : 'border-gray-200 hover:border-gray-300'
                  }`}
                >
                  <opt.icon className={`w-4 h-4 mb-1.5 ${expiryType === opt.value ? 'text-brand-600' : 'text-gray-400'}`} />
                  <p className={`text-xs font-semibold ${expiryType === opt.value ? 'text-brand-700' : 'text-gray-700'}`}>{opt.label}</p>
                  <p className="text-xs text-gray-400">{opt.desc}</p>
                </button>
              ))}
            </div>
            {expiryType === 'DAYS' && (
              <div className="mt-3 flex items-center gap-3">
                <input
                  type="number"
                  min={1}
                  max={365}
                  value={expiryDays}
                  onChange={(e) => setExpiryDays(parseInt(e.target.value) || 7)}
                  className="w-24 px-3 py-2 rounded-xl border border-gray-200 focus:border-brand-500 outline-none text-sm"
                />
                <span className="text-sm text-gray-500">days until link expires</span>
              </div>
            )}
          </div>

          {recipientEmail && (
            <label className="flex items-center gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={sendEmail}
                onChange={(e) => setSendEmail(e.target.checked)}
                className="w-4 h-4 rounded border-gray-300 text-brand-600 focus:ring-brand-500"
              />
              <div>
                <p className="text-sm font-medium text-gray-700">Send email notification</p>
                <p className="text-xs text-gray-400">Automatically email the link to {recipientEmail}</p>
              </div>
            </label>
          )}

          <div className="flex gap-3 pt-2">
            <button
              onClick={() => { setStep('upload'); setFile(null); setUploadedFile(null); }}
              className="flex-1 py-3 rounded-xl border border-gray-200 text-gray-600 font-medium text-sm hover:bg-gray-50 transition-colors"
            >
              Start over
            </button>
            <button
              onClick={handleSend}
              disabled={sending}
              className="flex-1 flex items-center justify-center gap-2 bg-brand-600 text-white font-semibold py-3 rounded-xl hover:bg-brand-700 disabled:opacity-60 transition-colors"
            >
              {sending ? (
                <><Loader2 className="w-4 h-4 animate-spin" /> Creating link...</>
              ) : (
                <><Send className="w-4 h-4" /> Generate secure link</>
              )}
            </button>
          </div>
        </div>
      )}

      {/* Step 3: Done */}
      {step === 'done' && shareResult && (
        <div className="bg-white rounded-2xl border border-gray-100 p-6">
          <div className="text-center mb-8">
            <div className="w-16 h-16 bg-green-50 rounded-full flex items-center justify-center mx-auto mb-4">
              <CheckCircle2 className="w-8 h-8 text-green-500" />
            </div>
            <h2 className="text-xl font-bold text-gray-900 mb-2">Secure link created!</h2>
            <p className="text-gray-500 text-sm">
              {sendEmail && recipientEmail
                ? `An email has been sent to ${recipientEmail} with this link.`
                : 'Copy the link below and share it with your recipient.'}
            </p>
          </div>

          <div className="bg-gray-50 rounded-xl p-4 mb-4">
            <p className="text-xs text-gray-400 font-medium uppercase tracking-wider mb-2">Secure link</p>
            <div className="flex items-center gap-2">
              <p className="text-sm text-gray-700 flex-1 break-all font-mono">{shareResult.shareUrl}</p>
              <button
                onClick={copyLink}
                className={`flex items-center gap-1.5 text-xs font-medium px-3 py-2 rounded-lg transition-all flex-shrink-0 ${
                  copied ? 'bg-green-100 text-green-700' : 'bg-white text-gray-600 hover:bg-gray-100 border border-gray-200'
                }`}
              >
                {copied ? <><Check className="w-3.5 h-3.5" /> Copied!</> : <><Copy className="w-3.5 h-3.5" /> Copy</>}
              </button>
            </div>
          </div>

          <div className="flex gap-3">
            <button
              onClick={() => {
                setStep('upload'); setFile(null); setUploadedFile(null);
                setShareResult(null); setRecipientEmail(''); setRecipientName('');
                setMessage(''); setExpiryType('NEVER'); setSendEmail(false);
              }}
              className="flex-1 py-3 rounded-xl border border-gray-200 text-gray-600 font-medium text-sm hover:bg-gray-50 transition-colors"
            >
              Upload another
            </button>
            <button
              onClick={() => router.push('/dashboard')}
              className="flex-1 py-3 rounded-xl bg-brand-600 text-white font-semibold text-sm hover:bg-brand-700 transition-colors"
            >
              Go to dashboard
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
