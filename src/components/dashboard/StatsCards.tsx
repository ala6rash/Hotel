import { FileText, Share2, Eye, ShieldCheck } from 'lucide-react'

interface Props {
  totalFiles: number
  totalShares: number
  activeShares: number
  totalViews: number
}

export function StatsCards({ totalFiles, totalShares, activeShares, totalViews }: Props) {
  const cards = [
    { label: 'Total Files', value: totalFiles, icon: FileText, color: 'brand' },
    { label: 'Total Shares', value: totalShares, icon: Share2, color: 'blue' },
    { label: 'Active Links', value: activeShares, icon: ShieldCheck, color: 'green' },
    { label: 'Total Views', value: totalViews, icon: Eye, color: 'purple' },
  ]

  const colorMap: Record<string, string> = {
    brand: 'bg-brand-50 text-brand-600',
    blue: 'bg-blue-50 text-blue-600',
    green: 'bg-green-50 text-green-600',
    purple: 'bg-purple-50 text-purple-600',
  }

  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
      {cards.map((card) => (
        <div key={card.label} className="bg-white rounded-2xl p-5 border border-gray-100">
          <div className={`w-10 h-10 rounded-xl flex items-center justify-center mb-3 ${colorMap[card.color]}`}>
            <card.icon className="w-5 h-5" />
          </div>
          <div className="text-2xl font-bold text-gray-900">{card.value.toLocaleString()}</div>
          <div className="text-sm text-gray-500 mt-0.5">{card.label}</div>
        </div>
      ))}
    </div>
  )
}
