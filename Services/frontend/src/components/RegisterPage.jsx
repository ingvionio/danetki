import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';

const RegisterPage = () => {
  const [email, setEmail] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

  const handleRegister = async (e) => {
    e.preventDefault();
    setError('');

    try {
      const response = await axios.post('http://localhost:8000/auth/register', {
        email: email,
        username: username,
        password: password
      });

      const { token, user_id } = response.data;
      localStorage.setItem('token', token);
      localStorage.setItem('userId', user_id);

      window.location.href = '/';
      
    } catch (err) {
      if (err.response && err.response.data) {
        setError(err.response.data.message || 'Request failed with status code ' + err.response.status);
      } else {
        setError('Ошибка при регистрации. Проверьте логи бэкенда.');
      }
    }
  };

  return (
    <div style={{ maxWidth: '400px', margin: '50px auto', textAlign: 'center', fontFamily: 'sans-serif' }}>
      <h2>Регистрация</h2>
      <form onSubmit={handleRegister} style={{ display: 'flex', flexDirection: 'column', gap: '15px' }}>
        <div style={{ textAlign: 'left' }}>
          <label>Email (уникальный):</label>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            style={{ width: '100%', padding: '8px', boxSizing: 'border-box' }}
          />
        </div>

        <div style={{ textAlign: 'left' }}>
          <label>Отображаемое имя (Username):</label>
          <input
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
            style={{ width: '100%', padding: '8px', boxSizing: 'border-box' }}
          />
        </div>

        <div style={{ textAlign: 'left' }}>
          <label>Пароль (минимум 8 символов):</label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
            style={{ width: '100%', padding: '8px', boxSizing: 'border-box' }}
          />
        </div>

        <button type="submit" style={{ padding: '10px', cursor: 'pointer' }}>Зарегистрироваться</button>
      </form>

      {error && <p style={{ color: 'red', marginTop: '15px' }}>{error}</p>}
      
      <p style={{ marginTop: '20px' }}>
        Уже есть аккаунт? <Link to="/login">Войти</Link>
      </p>
    </div>
  );
};

export default RegisterPage;