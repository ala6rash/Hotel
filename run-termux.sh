#!/bin/bash
# ─── Hotel SaaS — Termux Startup Script ──────────────────────────────────────
# Run this once: bash run-termux.sh
# Then open browser at: http://localhost:5173

set -e

echo ""
echo "╔══════════════════════════════════════════╗"
echo "║   Hotel Booking SaaS — Termux Setup      ║"
echo "╚══════════════════════════════════════════╝"
echo ""

# Check dotnet
if ! command -v dotnet &> /dev/null; then
    echo "⚙️  Installing .NET SDK..."
    pkg install dotnet-sdk-8.0 -y
fi

# Check node
if ! command -v node &> /dev/null; then
    echo "⚙️  Installing Node.js..."
    pkg install nodejs -y
fi

echo "✅ .NET version: $(dotnet --version)"
echo "✅ Node version: $(node --version)"
echo ""

# ─── Backend ─────────────────────────────────────────────────────────────────
echo "🚀 Starting backend (ASP.NET Core + SQLite)..."

BACKEND_DIR="$(dirname "$0")/src/HotelBookingSaaS.API"
cd "$BACKEND_DIR"

# Use Termux config (SQLite, no HTTPS)
export ASPNETCORE_ENVIRONMENT=Termux
export ASPNETCORE_URLS="http://0.0.0.0:5000"

dotnet run &
BACKEND_PID=$!

echo "⏳ Waiting for backend to start..."
for i in {1..30}; do
    if curl -s http://localhost:5000/health > /dev/null 2>&1; then
        echo "✅ Backend running at http://localhost:5000"
        break
    fi
    sleep 1
done

# ─── Frontend ─────────────────────────────────────────────────────────────────
echo ""
echo "🎨 Starting frontend (React + Vite)..."

FRONTEND_DIR="$(dirname "$0")/frontend"
cd "$FRONTEND_DIR"

if [ ! -d "node_modules" ]; then
    echo "⚙️  Installing npm packages (first time only)..."
    npm install
fi

npm run dev &
FRONTEND_PID=$!

echo ""
echo "══════════════════════════════════════════════"
echo "✅ Everything is running!"
echo ""
echo "   Open in browser:  http://localhost:5173"
echo "   API docs:         http://localhost:5000/swagger"
echo ""
echo "   Login:  admin@hotelsaas.jo"
echo "   Pass:   Admin@123"
echo "══════════════════════════════════════════════"
echo ""
echo "Press Ctrl+C to stop everything."

# Keep script alive and kill both on exit
trap "kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; echo 'Stopped.'" EXIT
wait
