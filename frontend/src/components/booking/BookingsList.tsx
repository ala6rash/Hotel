import { useEffect } from 'react'
import { useDispatch, useSelector } from 'react-redux'
import { AppDispatch, RootState } from '../../store'
import { fetchBookings, setFilters, setPage } from '../../store/slices/bookingsSlice'
import { format } from 'date-fns'
import { bookingsApi } from '../../services/api'
import toast from 'react-hot-toast'

const STATUS_COLORS: Record<number, string> = {
  1: 'bg-yellow-100 text-yellow-800',  // Pending
  2: 'bg-green-100 text-green-800',    // Confirmed
  3: 'bg-blue-100 text-blue-800',      // CheckedIn
  4: 'bg-gray-100 text-gray-800',      // CheckedOut
  5: 'bg-red-100 text-red-800',        // Cancelled
}

const OTA_ICONS: Record<string, string> = {
  BookingCom: '🏨',
  Airbnb: '🏠',
  Expedia: '✈️',
  Agoda: '🌏',
  Direct: '📋',
  Channex: '🔗',
}

export default function BookingsList() {
  const dispatch = useDispatch<AppDispatch>()
  const { items, totalCount, page, pageSize, loading, filters } = useSelector(
    (s: RootState) => s.bookings
  )
  const { user } = useSelector((s: RootState) => s.auth)

  useEffect(() => {
    dispatch(fetchBookings({
      ...filters,
      page,
      pageSize,
      hotelId: user?.hotelId,
    }))
  }, [filters, page, user?.hotelId, dispatch])

  const handleSync = async () => {
    if (!user?.hotelId) return
    try {
      await bookingsApi.triggerSync(user.hotelId)
      toast.success('Sync started — bookings will update shortly')
    } catch {
      toast.error('Failed to start sync')
    }
  }

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200">
      {/* Toolbar */}
      <div className="p-4 border-b flex flex-wrap gap-3 items-center justify-between">
        <h2 className="font-semibold text-gray-900">Bookings ({totalCount})</h2>

        <div className="flex gap-2 flex-wrap">
          <input
            type="text"
            placeholder="Search guest..."
            className="border rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            onChange={(e) => dispatch(setFilters({ guestName: e.target.value || undefined }))}
          />

          <select
            className="border rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            onChange={(e) => dispatch(setFilters({ status: e.target.value ? +e.target.value : undefined }))}
          >
            <option value="">All Statuses</option>
            <option value="1">Pending</option>
            <option value="2">Confirmed</option>
            <option value="3">Checked In</option>
            <option value="4">Checked Out</option>
            <option value="5">Cancelled</option>
          </select>

          <select
            className="border rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            onChange={(e) => dispatch(setFilters({ otaType: e.target.value ? +e.target.value : undefined }))}
          >
            <option value="">All Platforms</option>
            <option value="1">Booking.com</option>
            <option value="2">Airbnb</option>
            <option value="3">Expedia</option>
            <option value="4">Agoda</option>
            <option value="6">Direct</option>
          </select>

          <button
            onClick={handleSync}
            className="bg-blue-600 text-white px-3 py-1.5 rounded-lg text-sm hover:bg-blue-700 transition-colors"
          >
            ↻ Sync Now
          </button>
        </div>
      </div>

      {/* Table */}
      {loading ? (
        <div className="flex justify-center items-center h-40 text-gray-400">Loading...</div>
      ) : items.length === 0 ? (
        <div className="flex justify-center items-center h-40 text-gray-400">No bookings found</div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-gray-600 uppercase text-xs">
              <tr>
                <th className="px-4 py-3 text-left">Platform</th>
                <th className="px-4 py-3 text-left">Guest</th>
                <th className="px-4 py-3 text-left">Check-In</th>
                <th className="px-4 py-3 text-left">Check-Out</th>
                <th className="px-4 py-3 text-left">Nights</th>
                <th className="px-4 py-3 text-left">Room</th>
                <th className="px-4 py-3 text-right">Price</th>
                <th className="px-4 py-3 text-center">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {items.map((b) => (
                <tr key={b.bookingId} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">
                    <span title={b.otaTypeName}>
                      {OTA_ICONS[b.otaTypeName] ?? '📦'} {b.otaTypeName}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{b.guestName}</div>
                    {b.guestEmail && (
                      <div className="text-xs text-gray-500">{b.guestEmail}</div>
                    )}
                  </td>
                  <td className="px-4 py-3 text-gray-700">
                    {format(new Date(b.checkInDate), 'dd MMM yyyy')}
                  </td>
                  <td className="px-4 py-3 text-gray-700">
                    {format(new Date(b.checkOutDate), 'dd MMM yyyy')}
                  </td>
                  <td className="px-4 py-3 text-center text-gray-700">{b.nights}</td>
                  <td className="px-4 py-3 text-gray-700">{b.roomType ?? '—'}</td>
                  <td className="px-4 py-3 text-right font-medium text-gray-900">
                    {b.totalPrice.toFixed(2)} {b.currency}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-1 rounded-full text-xs font-medium ${STATUS_COLORS[b.status] ?? 'bg-gray-100'}`}>
                      {b.statusName}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination */}
      <div className="p-4 border-t flex items-center justify-between text-sm text-gray-600">
        <span>Showing {Math.min((page - 1) * pageSize + 1, totalCount)}–{Math.min(page * pageSize, totalCount)} of {totalCount}</span>
        <div className="flex gap-2">
          <button
            onClick={() => dispatch(setPage(page - 1))}
            disabled={page <= 1}
            className="px-3 py-1 border rounded disabled:opacity-40 hover:bg-gray-50"
          >
            ← Prev
          </button>
          <button
            onClick={() => dispatch(setPage(page + 1))}
            disabled={page * pageSize >= totalCount}
            className="px-3 py-1 border rounded disabled:opacity-40 hover:bg-gray-50"
          >
            Next →
          </button>
        </div>
      </div>
    </div>
  )
}
