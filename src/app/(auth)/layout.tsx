import Link from 'next/link'
import { Lock } from 'lucide-react'

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-gradient-to-br from-brand-50 via-white to-gray-50 flex flex-col">
      <div className="p-6">
        <Link href="/" className="inline-flex items-center gap-2 text-gray-900 hover:opacity-80 transition-opacity">
          <div className="w-8 h-8 bg-brand-600 rounded-lg flex items-center justify-center">
            <Lock className="w-4 h-4 text-white" />
          </div>
          <span className="font-bold text-lg">SecureDocs</span>
        </Link>
      </div>
      <div className="flex-1 flex items-center justify-center p-4">
        {children}
      </div>
    </div>
  )
}
