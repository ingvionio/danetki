import React, { useState } from 'react';
import axios from 'axios';
import { Link, useNavigate } from 'react-router-dom';

const LoginPage = ({ onLoginSuccess }) => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  
  const navigate = useNavigate();

  const handleLogin = async (e) => {
    e.preventDefault();
    setError('');

    try {
      const response = await axios.post('http://localhost:8000/auth/login', {
        email: email,
        password: password
      });

      const { token, user_id } = response.data;
      localStorage.setItem('token', token);
      localStorage.setItem('userId', user_id);
      
      if (onLoginSuccess) {
        onLoginSuccess();
      }
      
      navigate('/');
      
    } catch (err) {
      if (err.response && err.response.data) {
        setError(err.response.data.message || 'Ошибка авторизации');
      } else {
        setError('Network Error: не удалось связаться с сервером');
      }
    }
  };

  return (
    <div style={{ maxWidth: '400px', margin: '50px auto', textAlign: 'center', fontFamily: 'sans-serif' }}>
      <h2>АВТОРИЗАЦИЯ</h2>
      <form onSubmit={handleLogin} style={{ display: 'flex', flexDirection: 'column', gap: '15px' }}>
        <div style={{ textAlign: 'left' }}>
          <label>Имя пользователя (Email):</label>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            style={{ width: '100%', padding: '8px', boxSizing: 'border-box' }}
          />
        </div>

        <div style={{ textAlign: 'left' }}>
          <label>Пароль:</label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            style={{ width: '100%', padding: '8px', boxSizing: 'border-box' }}
          />
        </div>

        <button type="submit" style={{ padding: '10px', cursor: 'pointer' }}>Войти</button>
      </form>

      {error && <p style={{ color: 'red', marginTop: '15px' }}>{error}</p>}
      
      <p style={{ marginTop: '20px' }}>
        Нет аккаунта? <Link to="/register">Создать аккаунт</Link>
      </p>
    </div>
  );
};

export default LoginPage;