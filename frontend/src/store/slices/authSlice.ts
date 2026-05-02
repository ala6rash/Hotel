import { createSlice, createAsyncThunk, PayloadAction } from '@reduxjs/toolkit'
import { authApi } from '../../services/api'

interface AuthState {
  user: { userId: number; email: string; fullName: string; role: string; hotelId?: number } | null
  accessToken: string | null
  loading: boolean
  error: string | null
}

const initialState: AuthState = {
  user: JSON.parse(localStorage.getItem('user') || 'null'),
  accessToken: localStorage.getItem('accessToken'),
  loading: false,
  error: null,
}

export const login = createAsyncThunk(
  'auth/login',
  async ({ email, password }: { email: string; password: string }, { rejectWithValue }) => {
    try {
      const { data } = await authApi.login(email, password)
      return data.data
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.errors?.[0] || 'Login failed')
    }
  }
)

export const logout = createAsyncThunk('auth/logout', async () => {
  await authApi.logout().catch(() => {})
  localStorage.clear()
})

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(login.pending, (state) => {
        state.loading = true; state.error = null
      })
      .addCase(login.fulfilled, (state, action: PayloadAction<any>) => {
        state.loading = false
        state.user = {
          userId: action.payload.userId,
          email: action.payload.email,
          fullName: action.payload.fullName,
          role: action.payload.role,
          hotelId: action.payload.hotelId,
        }
        state.accessToken = action.payload.accessToken
        localStorage.setItem('accessToken', action.payload.accessToken)
        localStorage.setItem('refreshToken', action.payload.refreshToken)
        localStorage.setItem('user', JSON.stringify(state.user))
      })
      .addCase(login.rejected, (state, action) => {
        state.loading = false
        state.error = action.payload as string
      })
      .addCase(logout.fulfilled, (state) => {
        state.user = null; state.accessToken = null
      })
  },
})

export default authSlice.reducer
