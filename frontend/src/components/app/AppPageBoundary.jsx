import { Component } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState } from './ui'

// Keep workspace navigation available if a page render or lazy import fails.
export default class AppPageBoundary extends Component {
  state = { failed: false }

  static getDerivedStateFromError() { return { failed: true } }

  render() {
    if (!this.state.failed) return this.props.children
    return <div role="alert"><EmptyState title="Unable to display this page"
      description="Return to the dashboard, or reload to retry. Reloading resets local demo changes but keeps your sign-in."
      actions={<><button className="app-ui-button" onClick={() => window.location.reload()}>Reload page</button>
        <Link className="app-ui-button" to="/app/dashboard">Return to Dashboard</Link></>} /></div>
  }
}
