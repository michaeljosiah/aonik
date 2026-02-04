import { NavLink } from "react-router-dom";
import { useAuth } from "../../app/auth/AuthContext";

export const HeaderDashboard = () => {
  const { user } = useAuth();

  return (
    <header className="header-sub cd-morph-dropdown">
      <div className="header-top">
        <nav className="navbar navbar-expand-lg">
          <div className="container">
            <NavLink className="navbar-brand" to="/">
              <img src="/images/logo.png" alt="Logo" />
            </NavLink>
            <button className="nav-trigger" type="button">
              <span aria-hidden="true"></span>
            </button>
            <div className="main-nav navbar-collapse" id="navbar">
              <ul className="navbar-nav ml-auto">
                <li className="nav-item">
                  <NavLink className="nav-link" to="/help">
                    HELP &amp; SUPPORT
                  </NavLink>
                </li>
              </ul>
              <ul className="navbar-nav navbar-btn">
                <li>
                  <NavLink className="btn btn-primary btn-sm" to="/logout">
                    LOGOUT
                  </NavLink>
                </li>
              </ul>
            </div>
          </div>
        </nav>
        <div className="morph-dropdown-wrapper">
          <div className="dropdown-list container p-lg-0">
            <ul>
              <li>
                <h4 className="label text-center">
                  <NavLink to="/help">HELP &amp; SUPPORT</NavLink>
                </h4>
              </li>
                <li>
                  <NavLink className="label btn btn-primary w-100" to="/logout">
                    LOGOUT
                  </NavLink>
                </li>
            </ul>
            <div className="bg-layer" aria-hidden="true"></div>
          </div>
        </div>
      </div>
      <div className="container">
        <div className="row align-items-center">
          <div className="col-10 col-md-7 col-lg-8">
            <div className="dropdown-h d-inline-block position-relative py-3">
              <a
                className="dropdown-user d-inline-flex align-items-center text-decoration-none"
                href="#"
                id="dropdownUser"
                data-bs-toggle="dropdown"
                aria-expanded="true"
              >
                <img src="/images/profile-pic.png" alt="User" />
                <div>
                  <strong className="d-block">Welcome, {user?.fullName ?? "John Doe"}</strong>
                  <small>Last login: 05:15 PM Friday, 19 August, 2022</small>
                </div>
              </a>
              <ul className="dropdown-user-menu dropdown-menu text-small" aria-labelledby="dropdownUser">
                <li>
                  <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/personal">
                    <div className="icon-left">
                      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path
                          d="M12 12C13.1867 12 14.3467 11.6481 15.3334 10.9888C16.3201 10.3295 17.0892 9.39246 17.5433 8.2961C17.9974 7.19975 18.1162 5.99335 17.8847 4.82946C17.6532 3.66558 17.0818 2.59648 16.2426 1.75736C15.4035 0.918247 14.3344 0.346802 13.1705 0.115291C12.0067 -0.11622 10.8003 0.00259972 9.7039 0.456726C8.60754 0.910851 7.67047 1.67989 7.01118 2.66658C6.35189 3.65328 6 4.81331 6 6C6.00159 7.59081 6.63424 9.11602 7.75911 10.2409C8.88399 11.3658 10.4092 11.9984 12 12ZM12 2C12.7911 2 13.5645 2.2346 14.2223 2.67412C14.8801 3.11365 15.3928 3.73836 15.6955 4.46927C15.9983 5.20017 16.0775 6.00444 15.9231 6.78036C15.7688 7.55629 15.3878 8.26902 14.8284 8.82843C14.269 9.38784 13.5563 9.7688 12.7804 9.92314C12.0044 10.0775 11.2002 9.99827 10.4693 9.69552C9.73836 9.39277 9.11365 8.88008 8.67412 8.22228C8.2346 7.56449 8 6.79113 8 6C8 4.93914 8.42143 3.92172 9.17157 3.17158C9.92172 2.42143 10.9391 2 12 2V2Z"
                          fill="currentColor"
                        />
                        <path
                          d="M12 14C9.61386 14.0026 7.32622 14.9517 5.63896 16.639C3.95171 18.3262 3.00265 20.6139 3 23C3 23.2652 3.10536 23.5196 3.29289 23.7071C3.48043 23.8946 3.73478 24 4 24C4.26522 24 4.51957 23.8946 4.70711 23.7071C4.89464 23.5196 5 23.2652 5 23C5 21.1435 5.7375 19.363 7.05025 18.0503C8.36301 16.7375 10.1435 16 12 16C13.8565 16 15.637 16.7375 16.9497 18.0503C18.2625 19.363 19 21.1435 19 23C19 23.2652 19.1054 23.5196 19.2929 23.7071C19.4804 23.8946 19.7348 24 20 24C20.2652 24 20.5196 23.8946 20.7071 23.7071C20.8946 23.5196 21 23.2652 21 23C20.9974 20.6139 20.0483 18.3262 18.361 16.639C16.6738 14.9517 14.3861 14.0026 12 14V14Z"
                          fill="currentColor"
                        />
                      </svg>
                    </div>
                    <div>
                      <strong className="d-block">My personal details</strong>
                      <small>Edit your name, mobile number ...</small>
                    </div>
                  </NavLink>
                </li>
                <li>
                  <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/login-details">
                    <div className="icon-left">
                      <svg width="20" height="24" viewBox="0 0 20 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path
                          d="M17 8.424V7C17 6.08075 16.8189 5.1705 16.4672 4.32122C16.1154 3.47194 15.5998 2.70026 14.9497 2.05025C14.2997 1.40024 13.5281 0.884626 12.6788 0.532843C11.8295 0.18106 10.9193 0 10 0C9.08075 0 8.17049 0.18106 7.32122 0.532843C6.47194 0.884626 5.70026 1.40024 5.05025 2.05025C4.40024 2.70026 3.88463 3.47194 3.53284 4.32122C3.18106 5.1705 3 6.08075 3 7V8.424C2.10936 8.81271 1.35129 9.45252 0.818499 10.2652C0.285705 11.0779 0.00127838 12.0282 0 13V19C0.00158786 20.3256 0.528882 21.5964 1.46622 22.5338C2.40356 23.4711 3.6744 23.9984 5 24H15C16.3256 23.9984 17.5964 23.4711 18.5338 22.5338C19.4711 21.5964 19.9984 20.3256 20 19V13C19.9987 12.0282 19.7143 11.0779 19.1815 10.2652C18.6487 9.45252 17.8906 8.81271 17 8.424V8.424ZM5 7C5 5.67392 5.52678 4.40215 6.46447 3.46447C7.40215 2.52678 8.67392 2 10 2C11.3261 2 12.5979 2.52678 13.5355 3.46447C14.4732 4.40215 15 5.67392 15 7V8H5V7ZM18 19C18 19.7956 17.6839 20.5587 17.1213 21.1213C16.5587 21.6839 15.7956 22 15 22H5C4.20435 22 3.44129 21.6839 2.87868 21.1213C2.31607 20.5587 2 19.7956 2 19V13C2 12.2044 2.31607 11.4413 2.87868 10.8787C3.44129 10.3161 4.20435 10 5 10H15C15.7956 10 16.5587 10.3161 17.1213 10.8787C17.6839 11.4413 18 12.2044 18 13V19Z"
                          fill="currentColor"
                        />
                        <path
                          d="M10 14C9.73478 14 9.48043 14.1054 9.29289 14.2929C9.10536 14.4804 9 14.7348 9 15V17C9 17.2652 9.10536 17.5196 9.29289 17.7071C9.48043 17.8946 9.73478 18 10 18C10.2652 18 10.5196 17.8946 10.7071 17.7071C10.8946 17.5196 11 17.2652 11 17V15C11 14.7348 10.8946 14.4804 10.7071 14.2929C10.5196 14.1054 10.2652 14 10 14Z"
                          fill="currentColor"
                        />
                      </svg>
                    </div>
                    <div>
                      <strong className="d-block">My login details</strong>
                      <small>Edit your email, password ...</small>
                    </div>
                  </NavLink>
                </li>
                <li>
                  <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/notifications">
                    <div className="icon-left">
                      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path
                          d="M20.889 15.331L17.111 21.486C16.6957 22.1662 16.1307 22.7429 15.4591 23.1721C14.7875 23.6013 14.0268 23.8718 13.235 23.963C13.0262 23.9877 12.8162 24 12.606 24C11.5316 23.9976 10.4841 23.6641 9.606 23.045C8.69885 23.7257 7.57788 24.059 6.44617 23.9844C5.31446 23.9099 4.24689 23.4325 3.43686 22.6386C2.62683 21.8448 2.12793 20.7871 2.03054 19.6571C1.93316 18.5272 2.24373 17.3997 2.906 16.479L1.584 15.166C1.02169 14.6047 0.593844 13.9234 0.332583 13.1731C0.0713218 12.4229 -0.0165616 11.6232 0.0755321 10.8341C0.167626 10.045 0.437303 9.28698 0.864313 8.61704C1.29132 7.9471 1.86457 7.38261 2.541 6.96596L8.316 3.39996C9.88127 2.39863 11.7211 1.91324 13.5766 2.01208C15.4321 2.11092 17.21 2.78901 18.66 3.95096L20.322 2.29196C20.5095 2.10418 20.7639 1.99859 21.0293 1.9984C21.2947 1.99821 21.5492 2.10345 21.737 2.29096C21.9248 2.47846 22.0304 2.73288 22.0306 2.99825C22.0307 3.26361 21.9255 3.51818 21.738 3.70596L20.078 5.36396C21.191 6.75537 21.8628 8.44766 22.0071 10.2236C22.1514 11.9995 21.7617 13.7781 20.888 15.331H20.889Z"
                          fill="currentColor"
                        />
                      </svg>
                    </div>
                    <div>
                      <strong className="d-block">Notification settings</strong>
                      <small>Manage your notifications</small>
                    </div>
                  </NavLink>
                </li>
                <li>
                  <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/marketing">
                    <div className="icon-left">
                      <svg width="25" height="24" viewBox="0 0 25 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path
                          d="M17 0C16.7348 0 16.4804 0.105357 16.2929 0.292893C16.1054 0.48043 16 0.734784 16 1C16 3.949 13.417 5 11 5H4C2.93913 5 1.92172 5.42143 1.17157 6.17157C0.421427 6.92172 0 7.93913 0 9L0 11C0.00218416 11.5987 0.139462 12.1893 0.401602 12.7276C0.663743 13.2659 1.04399 13.7381 1.514 14.109L5.086 22.081C5.34004 22.6521 5.75417 23.1373 6.27827 23.4779C6.80237 23.8185 7.41396 23.9998 8.039 24C8.53631 23.9997 9.02565 23.875 9.46247 23.6373C9.89929 23.3996 10.2697 23.0564 10.54 22.639C10.8104 22.2215 10.972 21.7431 11.0103 21.2473C11.0485 20.7515 10.9621 20.2539 10.759 19.8L8.559 15H11C13.417 15 16 16.051 16 19C16 19.2652 16.1054 19.5196 16.2929 19.7071C16.4804 19.8946 16.7348 20 17 20C17.2652 20 17.5196 19.8946 17.7071 19.7071C17.8946 19.5196 18 19.2652 18 19V1C18 0.734784 17.8946 0.48043 17.7071 0.292893C17.5196 0.105357 17.2652 0 17 0V0Z"
                          fill="currentColor"
                        />
                      </svg>
                    </div>
                    <div>
                      <strong className="d-block">Marketing preferences</strong>
                      <small>Manage marketing communication</small>
                    </div>
                  </NavLink>
                </li>
              </ul>
            </div>
          </div>
          <div className="col-2 col-md-5 col-lg-4 text-md-end">
            <div className="envelope d-inline-flex align-items-center py-3">
              <p className="mb-0 d-none d-md-block">
                You have <strong className="text-primary">6</strong> new messages.
              </p>
              <button type="button" className="btn btn-inbox">
                <svg
                  width="24"
                  height="24"
                  viewBox="0 0 24 24"
                  fill="currentColor"
                  xmlns="http://www.w3.org/2000/svg"
                >
                  <path
                    d="M19 1H5C3.6744 1.00159 2.40356 1.52888 1.46622 2.46622C0.528882 3.40356 0.00158786 4.6744 0 6L0 18C0.00158786 19.3256 0.528882 20.5964 1.46622 21.5338C2.40356 22.4711 3.6744 22.9984 5 23H19C20.3256 22.9984 21.5964 22.4711 22.5338 21.5338C23.4711 20.5964 23.9984 19.3256 24 18V6C23.9984 4.6744 23.4711 3.40356 22.5338 2.46622C21.5964 1.52888 20.3256 1.00159 19 1V1ZM5 3H19C19.5988 3.00118 20.1835 3.18151 20.679 3.5178C21.1744 3.85409 21.5579 4.33095 21.78 4.887L14.122 12.546C13.5584 13.1073 12.7954 13.4225 12 13.4225C11.2046 13.4225 10.4416 13.1073 9.878 12.546L2.22 4.887C2.44215 4.33095 2.82561 3.85409 3.32105 3.5178C3.81648 3.18151 4.40121 3.00118 5 3V3ZM19 21H5C4.20435 21 3.44129 20.6839 2.87868 20.1213C2.31607 19.5587 2 18.7956 2 18V7.5L8.464 13.96C9.40263 14.8963 10.6743 15.422 12 15.422C13.3257 15.422 14.5974 14.8963 15.536 13.96L22 7.5V18C22 18.7956 21.6839 19.5587 21.1213 20.1213C20.5587 20.6839 19.7956 21 19 21Z"
                    fill="currentColor"
                  />
                </svg>
                <span className="alerts-envelope">
                  <span className="visually-hidden">New Massage</span>
                </span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
};
