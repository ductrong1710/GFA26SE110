import { useParams } from 'react-router-dom'

export default function AppPlaceholderPage({ title }) {
  const { id } = useParams()

  return <section className="app-placeholder">
    <h1>{title}</h1>
    {id && <p>Mã: {id}</p>}
    <p>Chức năng đang được phát triển.</p>
  </section>
}
