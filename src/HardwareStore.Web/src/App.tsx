import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { RequireAuth, RequireAdmin } from './components/ProtectedRoute';
import Layout from './components/Layout';
import Home from './pages/Home';
import Login from './pages/Login';
import Signup from './pages/Signup';
import Search from './pages/Search';
import SearchStatus from './pages/SearchStatus';
import Reports from './pages/Reports';
import ReportDetail from './pages/ReportDetail';
import Admin from './pages/Admin';
import JamiesSuperGoodMedia from './pages/JamiesSuperGoodMedia';
import { jamiesSuperGoodMediaPath } from './config/site';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Layout>
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/login" element={<Login />} />
            <Route path="/signup" element={<Signup />} />
            <Route path="/status/:id" element={<SearchStatus />} />
            <Route path="/public/report/:id" element={<ReportDetail isPublic />} />
            <Route path={jamiesSuperGoodMediaPath} element={<JamiesSuperGoodMedia />} />

            {/* Protected routes */}
            <Route element={<RequireAuth />}>
              <Route path="/search" element={<Search />} />
              <Route path="/reports" element={<Reports />} />
              <Route path="/reports/:id" element={<ReportDetail />} />
            </Route>

            {/* Admin routes */}
            <Route element={<RequireAdmin />}>
              <Route path="/admin" element={<Admin />} />
            </Route>
          </Routes>
        </Layout>
      </BrowserRouter>
    </AuthProvider>
  );
}
