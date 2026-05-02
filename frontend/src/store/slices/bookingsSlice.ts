import { createSlice, createAsyncThunk, PayloadAction } from '@reduxjs/toolkit'
import { bookingsApi } from '../../services/api'

export interface Booking {
  bookingId: number
  hotelId: number
  hotelName: string
  otaBookingId?: string
  otaType: number
  otaTypeName: string
  guestName: string
  guestEmail?: string
  guestPhone?: string
  checkInDate: string
  checkOutDate: string
  nights: number
  roomType?: string
  totalPrice: number
  currency: string
  status: number
  statusName: string
  createdDate: string
}

interface BookingsState {
  items: Booking[]
  totalCount: number
  page: number
  pageSize: number
  loading: boolean
  error: string | null
  filters: {
    hotelId?: number
    otaType?: number
    status?: number
    checkInFrom?: string
    checkInTo?: string
    guestName?: string
    sortBy: string
    sortDesc: boolean
  }
}

const initialState: BookingsState = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  loading: false,
  error: null,
  filters: { sortBy: 'CheckInDate', sortDesc: false },
}

export const fetchBookings = createAsyncThunk(
  'bookings/fetchAll',
  async (params: object, { rejectWithValue }) => {
    try {
      const { data } = await bookingsApi.getAll(params)
      return data.data
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.errors?.[0] || 'Failed to fetch bookings')
    }
  }
)

export const triggerSync = createAsyncThunk(
  'bookings/triggerSync',
  async (hotelId: number, { rejectWithValue }) => {
    try {
      const { data } = await bookingsApi.triggerSync(hotelId)
      return data
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.errors?.[0] || 'Sync failed')
    }
  }
)

const bookingsSlice = createSlice({
  name: 'bookings',
  initialState,
  reducers: {
    setFilters(state, action: PayloadAction<Partial<BookingsState['filters']>>) {
      state.filters = { ...state.filters, ...action.payload }
      state.page = 1
    },
    setPage(state, action: PayloadAction<number>) {
      state.page = action.payload
    },
    addBookingFromWebhook(state, action: PayloadAction<Booking>) {
      // Prepend real-time booking to list
      state.items = [action.payload, ...state.items]
      state.totalCount += 1
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchBookings.pending, (state) => { state.loading = true; state.error = null })
      .addCase(fetchBookings.fulfilled, (state, action: PayloadAction<any>) => {
        state.loading = false
        state.items = action.payload.items
        state.totalCount = action.payload.totalCount
      })
      .addCase(fetchBookings.rejected, (state, action) => {
        state.loading = false
        state.error = action.payload as string
      })
  },
})

export const { setFilters, setPage, addBookingFromWebhook } = bookingsSlice.actions
export default bookingsSlice.reducer
