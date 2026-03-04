(function ($) {
"use strict";
	$('.toggle-password').on('click', function() {
		$(this).toggleClass('icon-eye icon-eye-slash');
		let input = $($(this).attr('toggle'));
		if (input.attr('type') == 'password') {
			input.attr('type', 'text');
		}
		else {
			input.attr('type', 'password');
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

	
	$(".form").validate({
		errorPlacement: function ( error, element ) {
			if ( element.prop( "type" ) === "checkbox" ) {
				error.insertAfter( element.parent( "div" ) );
			} else {
				error.insertAfter( element );
			}
		}
	});
	$('.form .form_field').on('blur', function() {
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
  
	$("#search-input").on("keyup", function() {
		var value = $(this).val().toLowerCase();
		$("#flag-list .flag-list").filter(function() {
			$(this).toggle($(this).text().toLowerCase().indexOf(value) > -1)
		});
	});
	
	$(".banner-slider").slick({
		dots: false,
		slidesToShow: 1,
		infinite: true,
		centerMode: true,
		centerPadding: "50px",
		arrows: false,
		slidesToScroll: 1,
		responsive: [
			{
				breakpoint: 475,
				settings: {
					centerPadding: "20px",
				},
			},

			{
				breakpoint: 375,
				settings: {
					centerPadding: "15px",
				},
			},
		],
	}).on('setPosition', function () {
		$(this).find('.slick-slide').height('auto');
		var slickTrack = $(this).find('.slick-track');
		var slickTrackHeight = $(slickTrack).height();
		$(this).find('.slick-slide').css('height', slickTrackHeight + 'px');
    });
	
	//Slick slider
	$('.slider').slick({
		autoplay: false,
		autoplaySpeed: 3000,
		speed: 1000,
		infinite: true,
		slidesToShow: 1,
		slidesToScroll: 1,
		dots: true,
		arrows: false,
		prevArrow: '<span class="slick-prev"><svg width="14px" height="14px" viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M11.354 1.646a.5.5 0 0 1 0 .708L5.707 8l5.647 5.646a.5.5 0 0 1-.708.708l-6-6a.5.5 0 0 1 0-.708l6-6a.5.5 0 0 1 .708 0z"/></svg></span>',
		nextArrow: '<span class="slick-next"><svg width="14px" height="14px" viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path fill-rule="evenodd" d="M4.646 1.646a.5.5 0 0 1 .708 0l6 6a.5.5 0 0 1 0 .708l-6 6a.5.5 0 0 1-.708-.708L10.293 8 4.646 2.354a.5.5 0 0 1 0-.708z"/></svg></span>',
		responsive: [
			{ breakpoint: 991, settings: { slidesToShow: 1 } }
		]
	}).on('setPosition', function () {
		$(this).find('.slick-slide').height('auto');
		var slickTrack = $(this).find('.slick-track');
		var slickTrackHeight = $(slickTrack).height();
		$(this).find('.slick-slide').css('height', slickTrackHeight + 'px');
    });
	
	$(".select-box").select2({
		width: '100%',
		minimumResultsForSearch: -1
	});
	
})(jQuery);	