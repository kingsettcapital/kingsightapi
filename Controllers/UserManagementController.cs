using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/user-management")]
    public class UserManagementController : ControllerBase
    {
        private readonly IRoleService _roleService;
        private readonly IUserService _userService;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            IRoleService roleService,
            IUserService userService,
            ILogger<UserManagementController> logger)
        {
            _roleService = roleService;
            _userService = userService;
            _logger = logger;
        }

        // GET: api/user-management/roles
        [HttpGet("roles")]
        public async Task<ActionResult<List<RoleDto>>> GetRoles(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _roleService.GetAllAsync(cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles");
                return StatusCode(500, "An error occurred while retrieving roles.");
            }
        }

        // GET: api/user-management/roles/{roleId}
        [HttpGet("roles/{roleId:int}")]
        public async Task<ActionResult<RoleDto>> GetRole(int roleId, CancellationToken cancellationToken)
        {
            if (roleId <= 0)
            {
                return BadRequest("roleId must be a positive integer.");
            }

            try
            {
                var result = await _roleService.GetByIdAsync(roleId, cancellationToken);
                return result is null ? NotFound() : Ok(result);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving role {RoleId}", roleId);
                return StatusCode(500, "An error occurred while retrieving the role.");
            }
        }

        // POST: api/user-management/roles
        [HttpPost("roles")]
        public async Task<ActionResult<RoleDto>> CreateRole(
            [FromBody] RoleSaveRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.RoleName))
            {
                return BadRequest("RoleName is required.");
            }

            try
            {
                var roleId = await _roleService.CreateAsync(request, cancellationToken);
                var created = await _roleService.GetByIdAsync(roleId, cancellationToken);
                return CreatedAtAction(nameof(GetRole), new { roleId }, created);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return StatusCode(500, "An error occurred while creating the role.");
            }
        }

        // PUT: api/user-management/roles/{roleId}
        [HttpPut("roles/{roleId:int}")]
        public async Task<IActionResult> UpdateRole(int roleId,
            [FromBody] RoleUpdateRequest request,
            CancellationToken cancellationToken)
        {
            if (roleId <= 0)
            {
                return BadRequest("roleId must be a positive integer.");
            }

            if (request is null || string.IsNullOrWhiteSpace(request.RoleName))
            {
                return BadRequest("RoleName is required.");
            }

            try
            {
                var updated = await _roleService.UpdateAsync(roleId, request, cancellationToken);
                if (!updated)
                {
                    return NotFound();
                }

                var result = await _roleService.GetByIdAsync(roleId, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role {RoleId}", roleId);
                return StatusCode(500, "An error occurred while updating the role.");
            }
        }

        // DELETE: api/user-management/roles/{roleId}
        [HttpDelete("roles/{roleId:int}")]
        public async Task<IActionResult> DeleteRole(int roleId, CancellationToken cancellationToken)
        {
            if (roleId <= 0)
            {
                return BadRequest("roleId must be a positive integer.");
            }

            try
            {
                var deleted = await _roleService.DeleteAsync(roleId, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role {RoleId}", roleId);
                return StatusCode(500, "An error occurred while deleting the role.");
            }
        }

        // GET: api/user-management/users
        [HttpGet("users")]
        public async Task<ActionResult<List<UserDto>>> GetUsers(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _userService.GetAllAsync(cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, "An error occurred while retrieving users.");
            }
        }

        // GET: api/user-management/users/{userId}
        [HttpGet("users/{userId:int}")]
        public async Task<ActionResult<UserDto>> GetUser(int userId, CancellationToken cancellationToken)
        {
            if (userId <= 0)
            {
                return BadRequest("userId must be a positive integer.");
            }

            try
            {
                var result = await _userService.GetByIdAsync(userId, cancellationToken);
                return result is null ? NotFound() : Ok(result);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving the user.");
            }
        }

        // POST: api/user-management/users
        [HttpPost("users")]
        public async Task<ActionResult<UserDto>> CreateUser(
            [FromBody] UserSaveRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            var validationError = _userService.ValidateSaveRequest(request);
            if (validationError is not null)
            {
                return BadRequest(validationError);
            }

            try
            {
                var userId = await _userService.CreateAsync(request, cancellationToken);
                var created = await _userService.GetByIdAsync(userId, cancellationToken);
                return CreatedAtAction(nameof(GetUser), new { userId }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, "An error occurred while creating the user.");
            }
        }

        // PUT: api/user-management/users/{userId}
        [HttpPut("users/{userId:int}")]
        public async Task<IActionResult> UpdateUser(int userId,
            [FromBody] UserUpdateRequest request,
            CancellationToken cancellationToken)
        {
            if (userId <= 0)
            {
                return BadRequest("userId must be a positive integer.");
            }

            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            var validationError = _userService.ValidateUpdateRequest(request);
            if (validationError is not null)
            {
                return BadRequest(validationError);
            }

            try
            {
                var updated = await _userService.UpdateAsync(userId, request, cancellationToken);
                if (!updated)
                {
                    return NotFound();
                }

                var result = await _userService.GetByIdAsync(userId, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", userId);
                return StatusCode(500, "An error occurred while updating the user.");
            }
        }

        // DELETE: api/user-management/users/{userId}
        [HttpDelete("users/{userId:int}")]
        public async Task<IActionResult> DeleteUser(int userId, CancellationToken cancellationToken)
        {
            if (userId <= 0)
            {
                return BadRequest("userId must be a positive integer.");
            }

            try
            {
                var deleted = await _userService.DeleteAsync(userId, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return StatusCode(500, "An error occurred while deleting the user.");
            }
        }
    }
}
