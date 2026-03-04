(function ($) {
"use strict";
	//PreLoader Js
	$(window).on('load',function() {
		$("#loading").fadeOut(500);
	});
	
 
	//Modal Show
	if ( $('#login , #register').length ) {
		var myModal = new bootstrap.Modal(document.getElementById("register"), {});
		document.onreadystatechange = function () {
			myModal.show();
		};	
	};
	
	//Sticky nav
    $(window).on('scroll',function() {    
        var scroll = $(window).scrollTop();
         if (scroll < 90) {
          $(".header, .header-top").removeClass("sticky");
         }else{
          $(".header, .header-top").addClass("sticky");
         }
		 
		 if($('.navcolumn-sticky').length != 0) {
			var scrolloffset = $('.navcolumn-sticky').offset().top;
			if(scroll > scrolloffset) {
				$('.navcolumn-sticky').addClass('fixed')
			}
		}
    }); 
	
	document.addEventListener("DOMContentLoaded", function (event) {
		function OTPInput() {
			const inputs = document.querySelectorAll("#otp > *[id]");
			for (let i = 0; i < inputs.length; i++) {
				inputs[i].addEventListener("keydown", function (event) {
					if (event.key === "Backspace") {
						inputs[i].value = "";
						if (i !== 0) inputs[i - 1].focus();
					} else {
						if (i === inputs.length - 1 && inputs[i].value !== "") {
							return true;
						} else if (event.keyCode > 47 && event.keyCode < 58) {
							inputs[i].value = event.key;
							if (i !== inputs.length - 1) inputs[i + 1].focus();
							event.preventDefault();
						} else if (event.keyCode > 64 && event.keyCode < 91) {
							inputs[i].value = String.fromCharCode(event.keyCode);
							if (i !== inputs.length - 1) inputs[i + 1].focus();
							event.preventDefault();
						}
					}
				});
			}
		}
		OTPInput();
	});

	$('.form .form-control').on('blur', function() {
		if( !$(this).val() ) {
			$(this).removeClass("not-empty");
		} else{
			$(this).addClass("not-empty");
		}
	});
	// listen to the keydown event which will bubble up to the containing 
	// form element
	$('.form-complete').on('keydown', function() {
		// set a flag
		var hasAnEmptyField = false;
		// iterate through each 'required input' and check if it's value 
		// is 'falsey' (i.e. empty)
		$('.form_field ,.form-control').each(function() {
			if (!this.value) {
			  // update the flag if this input has no value
			  hasAnEmptyField = true
			}
		});

		// use the flag to decide whether or not to have a class on the 
		//form element
		if (hasAnEmptyField) {
			$('.form-complete').addClass('is-incomplete')
		    // could also disable the button here.
		} else {
			$('.form-complete').removeClass('is-incomplete'),
			$('.form-complete .btn').removeClass('disabled'),
			$('.form-complete .bullet').removeClass('disabled')
		    // optionally re-enable button here.
		}
	});	
	
	//switch opacity
	$("#switch").change(function () {
		if ($(this).is(":checked")) {
			$('.switch-content').removeClass("disabled");
		} else {
			$('.switch-content').addClass('disabled');
		}
	}).change();
	
	
	//Tooltip
	var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
	var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
	  return new bootstrap.Tooltip(tooltipTriggerEl)
	});	

	//Smooth Scroll
	 $('a.scroll-down[href*="#"]:not([href="#"])').on('click', function() {
		if (location.pathname.replace(/^\//,'') == this.pathname.replace(/^\//,'') || location.hostname == this.hostname) {
		  var target = $(this.hash);
			  target = target.length ? target : $('[name=' + this.hash.slice(1) +']');
			  if (target.length) {
				$('html,body').animate({
				  scrollTop: target.offset().top,
				  }, 1000);
				  return false;
			  }
		}
	});
	
	$('.toggle-password').on('click', function() {
		$(this).toggleClass('icon-eye-slash');
		let input = $($(this).attr('toggle'));
		if (input.attr('type') == 'password') {
			input.attr('type', 'text');
		}
		else {
			input.attr('type', 'password');
		}
	});
	
   $(".select-box").select2({
		width: '100%',
		minimumResultsForSearch: -1
	});
	
	//select2 country
	function format(item) {
	  if (!item.id) {
		return item.text;
	  }
	  var countryUrl = "images/flags/";
	  var url = countryUrl;
	  var img = $("<img>", {
		class: "rounded-circle me-3",
		width: 32,
		src: url + item.element.value.toLowerCase() + ".svg"
	  });
	  var span = $("<span>", {
		text: " " + item.text
	  });
	  span.prepend(img);
	  return span;
	}

   $(".countries").select2({
		width: '100%',
		templateSelection: function(item) {
		  return format(item, false); 
		},
		templateResult: function(item) {
		  return format(item, false);
		}
	});
	
	//select2 categories
	var optionFormat = function(item1) {
		if ( !item1.id ) {
			return item1.text;
		}

		var span = document.createElement('span');
		var imgUrl = item1.element.getAttribute('data-img');
		var template = '';

		template += '<img src="' + imgUrl + '" class="rounded-circle me-3" alt="img"/>';
		template += item1.text;

		span.innerHTML = template;

		return $(span);
	}

	$('#categories').select2({
		width: '100%',
		templateSelection: optionFormat,
		templateResult: optionFormat,
		minimumResultsForSearch: -1
	});	
	
	//select2 categories
	var optionFormat1 = function(item2) {
		if ( !item2.id ) {
			return item2.text;
		}

		var span = document.createElement('span');
		var imgUrl = item2.element.getAttribute('data-img');
		var template = '';

		template += '<img src="' + imgUrl + '" class="me-3" alt="img"/>';
		template += item2.text;

		span.innerHTML = template;

		return $(span);
	}

	$('.categories').select2({
		width: '100%',
		templateSelection: optionFormat1,
		templateResult: optionFormat1,
		minimumResultsForSearch: -1
	});	
	
	
	if ( $( "#phone" ).length ) {
		var input = document.querySelector("#phone");
		window.intlTelInput(input, {
		  // allowDropdown: false,
		  // autoHideDialCode: false,
		  // autoPlaceholder: "off",
		  // dropdownContainer: document.body,
		  excludeCountries: ["us"],
		  // formatOnDisplay: false,
		  // geoIpLookup: function(callback) {
		  //   $.get("http://ipinfo.io", function() {}, "jsonp").always(function(resp) {
		  //     var countryCode = (resp && resp.country) ? resp.country : "";
		  //     callback(countryCode);
		  //   });
		  // },
		  // hiddenInput: "full_number",
		  // initialCountry: "auto",
		  // localizedCountries: { 'de': 'Deutschland' },
		  // nationalMode: false,
		  // onlyCountries: ['us', 'gb', 'ch', 'ca', 'do'],
		  // placeholderNumberType: "MOBILE",
		  // preferredCountries: ['cn', 'jp'],
		  separateDialCode: true,
		  utilsScript: "js/utils.js",
		});
	}
	
	//Slick slider
	$('.card-slider').slick({
		autoplay: false,
		infinite: true,
		speed: 500,
		slidesToShow: 2,
		slidesToScroll: 2,
		dots: true,
		arrows: true,
		prevArrow: '<svg class="slick-prev" width="17" height="26" viewBox="0 0 17 26" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M12.7283 25.4555L16.9709 21.2129L4.24303 8.48497L0.000384808 12.7276L12.7283 25.4555Z" fill="currentColor"/><path d="M0.000279307 12.7281L4.24292 16.9707L16.9708 4.24278L12.7282 0.000140667L0.000279307 12.7281Z" fill="currentColor"/></svg>',
		nextArrow: '<svg class="slick-next" width="17" height="26" viewBox="0 0 17 26" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M16.9709 12.729L12.7283 8.48633L0.000349641 21.2142L4.24299 25.4569L16.9709 12.729Z" fill="currentColor"/><path d="M4.24313 0.00150001L0.000488281 4.24414L12.7284 16.9721L16.9711 12.7294L4.24313 0.00150001Z" fill="currentColor"/></svg>',
		responsive: [
			{ breakpoint: 1199, settings: { slidesToShow: 2,  slidesToScroll: 2, arrows: true, dots: true } },
			{ breakpoint: 991, settings: { slidesToShow: 2,  slidesToScroll: 2, arrows: true, dots: true } },
			{ breakpoint: 767, settings: { slidesToShow: 1, slidesToScroll: 1, arrows: true, dots: true, autoplay: false } }
		]
	}).on('setPosition', function () {
		$(this).find('.slick-slide').height('auto');
		var slickTrack = $(this).find('.slick-track');
		var slickTrackHeight = $(slickTrack).height();
		$(this).find('.slick-slide').css('height', slickTrackHeight + 'px');
    });		
	
	//Slick slider
	$('.profile-slider').slick({
		autoplay: false,
		infinite: true,
		speed: 500,
		slidesToShow: 3,
		slidesToScroll: 3,
		dots: true,
		arrows: true,
		prevArrow: '<svg class="slick-prev" width="17" height="26" viewBox="0 0 17 26" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M12.7283 25.4555L16.9709 21.2129L4.24303 8.48497L0.000384808 12.7276L12.7283 25.4555Z" fill="currentColor"/><path d="M0.000279307 12.7281L4.24292 16.9707L16.9708 4.24278L12.7282 0.000140667L0.000279307 12.7281Z" fill="currentColor"/></svg>',
		nextArrow: '<svg class="slick-next" width="17" height="26" viewBox="0 0 17 26" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M16.9709 12.729L12.7283 8.48633L0.000349641 21.2142L4.24299 25.4569L16.9709 12.729Z" fill="currentColor"/><path d="M4.24313 0.00150001L0.000488281 4.24414L12.7284 16.9721L16.9711 12.7294L4.24313 0.00150001Z" fill="currentColor"/></svg>',
		responsive: [
			{ breakpoint: 1199, settings: { slidesToShow: 2,  slidesToScroll: 2, arrows: true, dots: true } },
			{ breakpoint: 991, settings: { slidesToShow: 2,  slidesToScroll: 2, arrows: true, dots: true } },
			{ breakpoint: 767, settings: { slidesToShow: 1, slidesToScroll: 1, arrows: false, dots: true, autoplay: false } }
		]
	}).on('setPosition', function () {
		$(this).find('.slick-slide').height('auto');
		var slickTrack = $(this).find('.slick-track');
		var slickTrackHeight = $(slickTrack).height();
		$(this).find('.slick-slide').css('height', slickTrackHeight + 'px');
    });	

	function morphDropdown( element ) {
		this.element = element;
		this.mainNavigation = this.element.find('.main-nav');
		this.mainNavigationItems = this.mainNavigation.find('.has-dropdown');
		this.dropdownList = this.element.find('.dropdown-list');
		this.dropdownWrappers = this.dropdownList.find('.qdropdown-nav');
		this.dropdownItems = this.dropdownList.find('.content');
		this.dropdownBg = this.dropdownList.find('.bg-layer');
		this.bindEvents();
	}

	morphDropdown.prototype.checkMq = function() {
		//check screen size
		var self = this;
		return window.getComputedStyle(self.element.get(0), '::before').getPropertyValue('content').replace(/'/g, "").replace(/"/g, "").split(', ');
	};

	morphDropdown.prototype.bindEvents = function() {
		var self = this;
		//hover over an item in the main navigation
		this.mainNavigationItems.mouseenter(function(event){
			//hover over one of the nav items -> show dropdown
			self.showDropdown($(this));
		}).mouseleave(function(){
			setTimeout(function(){
				//if not hovering over a nav item or a dropdown -> hide dropdown
				if( self.mainNavigation.find('.has-dropdown:hover').length == 0 && self.element.find('.dropdown-list:hover').length == 0 ) self.hideDropdown();
			}, 50);
		});
		
		//hover over the dropdown
		this.dropdownList.mouseleave(function(){
			setTimeout(function(){
				//if not hovering over a dropdown or a nav item -> hide dropdown
				(self.mainNavigation.find('.has-dropdown:hover').length == 0 && self.element.find('.dropdown-list:hover').length == 0 ) && self.hideDropdown();
			}, 50);
		});

		//click on an item in the main navigation -> open a dropdown on a touch device
		this.mainNavigationItems.on('touchstart', function(event){
			var selectedDropdown = self.dropdownList.find('#'+$(this).data('content'));
			if( !self.element.hasClass('is-dropdown-visible') || !selectedDropdown.hasClass('active') ) {
				event.preventDefault();
				self.showDropdown($(this));
			}
		});

		//on small screens, open navigation clicking on the menu icon
		this.element.on('click', '.nav-trigger', function(event){
			event.preventDefault();
			self.element.toggleClass('nav-open');
		});
	};

	morphDropdown.prototype.showDropdown = function(item) {
		this.mq = this.checkMq();
		if( this.mq == 'desktop') {
			var self = this;
			var selectedDropdown = this.dropdownList.find('#'+item.data('content')),
				selectedDropdownHeight = selectedDropdown.innerHeight(),
				selectedDropdownWidth = selectedDropdown.children('.content').innerWidth(),
				selectedDropdownLeft = item.offset().left + item.innerWidth()/2 - selectedDropdownWidth/2;

			//update dropdown position and size
			this.updateDropdown(selectedDropdown, parseInt(selectedDropdownHeight), selectedDropdownWidth, parseInt(selectedDropdownLeft));
			//add active class to the proper dropdown item
			this.element.find('.active').removeClass('active');
			selectedDropdown.addClass('active').removeClass('move-left move-right').prevAll().addClass('move-left').end().nextAll().addClass('move-right');
			item.addClass('active');
			//show the dropdown wrapper if not visible yet
			if( !this.element.hasClass('is-dropdown-visible') ) {
				setTimeout(function(){
					self.element.addClass('is-dropdown-visible');
				}, 10);
			}
		}
	};

	morphDropdown.prototype.updateDropdown = function(dropdownItem, height, width, left) {
		this.dropdownList.css({
		    '-moz-transform': 'translateX(' + left + 'px)',
		    '-webkit-transform': 'translateX(' + left + 'px)',
			'-ms-transform': 'translateX(' + left + 'px)',
			'-o-transform': 'translateX(' + left + 'px)',
			'transform': 'translateX(' + left + 'px)',
			'width': width+'px',
			'height': height+'px'
		});

		this.dropdownBg.css({
			'-moz-transform': 'scaleX(' + width + ') scaleY(' + height + ')',
		    '-webkit-transform': 'scaleX(' + width + ') scaleY(' + height + ')',
			'-ms-transform': 'scaleX(' + width + ') scaleY(' + height + ')',
			'-o-transform': 'scaleX(' + width + ') scaleY(' + height + ')',
			'transform': 'scaleX(' + width + ') scaleY(' + height + ')'
		});
	};

	morphDropdown.prototype.hideDropdown = function() {
		this.mq = this.checkMq();
		if( this.mq == 'desktop') {
			this.element.removeClass('is-dropdown-visible').find('.active').removeClass('active').end().find('.move-left').removeClass('move-left').end().find('.move-right').removeClass('move-right');
		}
	};

	morphDropdown.prototype.resetDropdown = function() {
		this.mq = this.checkMq();
		if( this.mq == 'mobile') {
			this.dropdownList.removeAttr('style');
		}
	};

	var morphDropdowns = [];
	if( $('.cd-morph-dropdown').length > 0 ) {
		$('.cd-morph-dropdown').each(function(){
			//create a morphDropdown object for each .cd-morph-dropdown
			morphDropdowns.push(new morphDropdown($(this)));
		});

		var resizing = false;

		//on resize, reset dropdown style property
		updateDropdownPosition();
		$(window).on('resize', function(){
			if( !resizing ) {
				resizing =  true;
				(!window.requestAnimationFrame) ? setTimeout(updateDropdownPosition, 300) : window.requestAnimationFrame(updateDropdownPosition);
			}
		});

		function updateDropdownPosition() {
			morphDropdowns.forEach(function(element){
				element.resetDropdown();
			});

			resizing = false;
		};
	}
})(jQuery);	