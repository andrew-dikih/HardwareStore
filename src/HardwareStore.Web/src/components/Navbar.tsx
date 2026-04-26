import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function Navbar() {
  const { user, isAuthenticated, isAdmin, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  return (
    <nav className="bg-orange-600 text-white shadow-lg">
      <div className="max-w-5xl mx-auto px-4 py-3 flex items-center justify-between">
        <Link to="/" className="text-xl font-bold tracking-tight flex items-center gap-2">
          🔧 HardwareStore
        </Link>
        <div className="flex items-center gap-2 text-sm">
          {isAuthenticated ? (
            <>
              <Link
                to="/search"
                className="px-3 py-1.5 rounded hover:bg-orange-700 transition"
              >
                Search
              </Link>
              <Link
                to="/reports"
                className="px-3 py-1.5 rounded hover:bg-orange-700 transition"
              >
                Reports
              </Link>
              {isAdmin && (
                <Link
                  to="/admin"
                  className="px-3 py-1.5 rounded hover:bg-orange-700 transition"
                >
                  Admin
                </Link>
              )}
              <span className="text-orange-200 hidden sm:inline">
                {user?.displayName}
              </span>
              <button
                onClick={handleLogout}
                className="px-3 py-1.5 rounded bg-orange-800 hover:bg-orange-900 transition"
              >
                Logout
              </button>
            </>
          ) : (
            <>
              <Link
                to="/login"
                className="px-3 py-1.5 rounded hover:bg-orange-700 transition"
              >
                Login
              </Link>
              <Link
                to="/signup"
                className="px-3 py-1.5 rounded bg-white text-orange-600 font-semibold hover:bg-orange-50 transition"
              >
                Sign Up
              </Link>
            </>
          )}
        </div>
      </div>
    </nav>
  );
}
