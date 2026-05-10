import Link from 'next/link'
import { Upload, FileText } from 'lucide-react'

export function EmptyState() {
  return (
    <div className="bg-white rounded-2xl border border-gray-100 border-dashed p-16 text-center">
      <div className="w-16 h-16 bg-gray-50 rounded-2xl flex items-center justify-center mx-auto mb-4">
        <FileText className="w-8 h-8 text-gray-300" />
      </div>
      <h3 className="text-lg font-semibold text-gray-900 mb-2">No documents yet</h3>
      <p className="text-gray-400 text-sm mb-6 max-w-xs mx-auto">
        Upload your first document and generate a secure share link to send to recipients.
      </p>
      <Link
        href="/upload"
        className="inline-flex items-center gap-2 bg-brand-600 text-white font-medium px-5 py-2.5 rounded-xl hover:bg-brand-700 transition-colors text-sm"
      >
        <Upload className="w-4 h-4" />
        Upload your first file
      </Link>
    </div>
  )
}
