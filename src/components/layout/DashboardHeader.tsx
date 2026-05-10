'use client'

import { signOut } from 'next-auth/react'
import Link from 'next/link'
import { LogOut, Upload, User } from 'lucide-react'

interface Props {
  user: { name?: string | null; email?: string | null }
}

export function DashboardHeader({ user }: Props) {
  return (
    <header className="bg-white border-b border-gray-100 px-6 py-4 flex items-center justify-between">
      <div className="lg:hidden">
        <Link href="/dashboard" className="font-bold text-gray-900">SecureDocs</Link>
      </div>
      <div className="hidden lg:block" />
      <div className="flex items-center gap-4">
        <Link
          href="/upload"
          className="flex items-center gap-2 bg-brand-600 text-white text-sm font-medium px-4 py-2 rounded-lg hover:bg-brand-700 transition-colors"
        >
          <Upload className="w-4 h-4" />
          Upload file
        </Link>
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-gray-100 rounded-full flex items-center justify-center">
            <User className="w-4 h-4 text-gray-500" />
          </div>
          <div className="hidden sm:block">
            <p className="text-sm font-medium text-gray-900 leading-none">{user.name || 'User'}</p>
            <p className="text-xs text-gray-400 mt-0.5">{user.email}</p>
          </div>
        </div>
        <button
          onClick={() => signOut({ callbackUrl: '/login' })}
          className="p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors"
          title="Sign out"
        >
          <LogOut className="w-4 h-4" />
        </button>
      </div>
    </header>
  )
}
