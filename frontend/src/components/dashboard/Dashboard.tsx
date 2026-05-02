import { useEffect, useState } from 'react'
import { useSelector } from 'react-redux'
import { RootState } from '../../store'
import { hotelsApi } from '../../services/api'
import { useSignalR } from '../../hooks/useSignalR'
import BookingsList from '../booking/BookingsList'
import { format } from 'date-fns'

export default function Dashboard() {
  const { user } = useSelector((s: RootState) => s.auth)
  const [stats, setStats] = useState<any>(null)

  useSignalR(user?.hotelId)

  useEffect(() => {
    if (user?.hotelId) {
      hotelsApi.getStats(user.hotelId).then(({ data }) => setStats(data.data))
    }
  }, [user?.hotelId])

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
          <p className="text-sm text-gray-500">{format(new Date(), 'EEEE, dd MMMM yyyy')}</p>
        </div>
      </div>

      {/* Stats cards */}
      {stats && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <StatCard label="Total Bookings" value={stats.totalBookings} color="blue" />
          <StatCard label="Active Today" value={stats.activeToday} color="green" />
          <StatCard label="Check-ins Today" value={stats.checkinsToday} color="purple" />
          <StatCard label="Check-outs Today" value={stats.checkoutsToday} color="orange" />
        </div>
      )}

      {/* Last sync */}
      {stats?.lastSyncTime && (
        <p className="text-xs text-gray-400">
          Last sync: {format(new Date(stats.lastSyncTime), 'dd MMM yyyy HH:mm')} UTC
        </p>
      )}

      {/* Bookings list */}
      <BookingsList />
    </div>
  )
}

function StatCard({ label, value, color }: { label: string; value: number; color: string }) {
  const colors: Record<string, string> = {
    blue: 'bg-blue-50 text-blue-700 border-blue-200',
    green: 'bg-green-50 text-green-700 border-green-200',
    purple: 'bg-purple-50 text-purple-700 border-purple-200',
    orange: 'bg-orange-50 text-orange-700 border-orange-200',
  }
  return (
    <div className={`rounded-xl border p-4 ${colors[color]}`}>
      <p className="text-sm font-medium opacity-70">{label}</p>
      <p className="text-3xl font-bold mt-1">{value ?? 0}</p>
    </div>
  )
}
