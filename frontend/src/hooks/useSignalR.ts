import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { useDispatch } from 'react-redux'
import { addBookingFromWebhook } from '../store/slices/bookingsSlice'
import toast from 'react-hot-toast'

export function useSignalR(hotelId: number | undefined) {
  const dispatch = useDispatch()
  const connectionRef = useRef<signalR.HubConnection | null>(null)

  useEffect(() => {
    if (!hotelId) return

    const token = localStorage.getItem('accessToken')
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/booking', {
        accessTokenFactory: () => token || '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connectionRef.current = connection

    connection.on('NewBooking', (data) => {
      toast.success(`New booking: ${data.booking.guestName}`, { duration: 5000 })
      dispatch(addBookingFromWebhook(data.booking))
    })

    connection.on('SyncComplete', (data) => {
      if (data.created > 0) {
        toast.success(`Sync: ${data.created} new, ${data.updated} updated`)
      }
    })

    connection.start().then(() => {
      connection.invoke('JoinHotelGroup', hotelId).catch(console.error)
    }).catch(console.error)

    return () => {
      connection.invoke('LeaveHotelGroup', hotelId).catch(() => {})
      connection.stop()
    }
  }, [hotelId, dispatch])

  return connectionRef
}
