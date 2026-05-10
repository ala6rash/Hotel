import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'
import { ShareStatus, ExpiryType } from '@prisma/client'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
}

export function formatDate(date: Date | string): string {
  return new Date(date).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}

export function formatDateTime(date: Date | string): string {
  return new Date(date).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
}

export function getShareStatusLabel(
  status: ShareStatus,
  expiresAt: Date | null,
  viewOnce: boolean,
  viewCount: number
): string {
  if (status === ShareStatus.REVOKED) return 'Revoked'
  if (status === ShareStatus.EXPIRED) return 'Expired'
  if (expiresAt && new Date() > expiresAt) return 'Expired'
  if (viewOnce && viewCount >= 1) return 'Viewed'
  if (viewCount > 0) return 'Viewed'
  return 'Sent'
}

export function isShareAccessible(share: {
  status: ShareStatus
  expiresAt: Date | null
  viewOnce: boolean
  viewCount: number
}): boolean {
  if (share.status !== ShareStatus.ACTIVE) return false
  if (share.expiresAt && new Date() > share.expiresAt) return false
  if (share.viewOnce && share.viewCount >= 1) return false
  return true
}

export const ALLOWED_MIME_TYPES = [
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.ms-powerpoint',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation',
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/webp',
  'text/plain',
  'application/zip',
]

export const MAX_FILE_SIZE = 100 * 1024 * 1024 // 100MB
