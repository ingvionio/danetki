import axios from 'axios';

const baseURL = import.meta.env.VITE_API_GATEWAY_URL || 'http://localhost:8000';

const api = axios.create({
  baseURL: baseURL,
  timeout: 5000,
});

// Перехватчик для автоматического добавления JWT-токена
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;