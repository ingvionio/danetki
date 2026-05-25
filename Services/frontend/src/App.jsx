import React, { useState, useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import LoginPage from './components/LoginPage';
import RegisterPage from './components/RegisterPage';
import MainPage from './components/MainPage';

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(!!localStorage.getItem('token'));

  const handleLoginSuccess = () => {
    setIsAuthenticated(true);
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('userId');
    setIsAuthenticated(false);
  };

  return (
    <Router>
      <Routes>
        {/* Главная страница: если авторизован — показываем MainPage, если нет — отправляем на логин */}
        <Route 
          path="/" 
          element={isAuthenticated ? <MainPage onLogout={handleLogout} /> : <Navigate to="/login" />} 
        />
        
        {/* Страница логина: передаем функцию обновления состояния */}
        <Route 
          path="/login" 
          element={!isAuthenticated ? <LoginPage onLoginSuccess={handleLoginSuccess} /> : <Navigate to="/" />} 
        />
        
        {/* Страница регистрации: теперь тоже проверяет авторизацию */}
        <Route 
          path="/register" 
          element={!isAuthenticated ? <RegisterPage /> : <Navigate to="/" />} 
        />
        
        {/* Все остальные ссылки кидают на главную */}
        <Route path="*" element={<Navigate to="/" />} />
      </Routes>
    </Router>
  );
}

export default App;